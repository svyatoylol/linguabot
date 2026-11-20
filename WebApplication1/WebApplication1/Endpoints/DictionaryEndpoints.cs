using LinguaBot.Models;
using LinguaBot.Services;

namespace LinguaBot.Endpoints;

public static class DictionaryEndpoints
{
    public static void MapDictionaryEndpoints(this WebApplication app)
    {
        // 1. Получение слов из личного словаря
        app.MapGet("/api/dictionary", (string? language, HttpContext ctx, LinguaDataStore store) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);
            var query = store.DictionaryWords.Values.Where(w => w.UserId == user.Id);

            if (!string.IsNullOrWhiteSpace(language))
            {
                query = query.Where(w => w.LanguageCode.Equals(language, StringComparison.OrdinalIgnoreCase));
            }

            var words = query.OrderByDescending(w => w.AddedAt).Select(w => new
            {
                w.Id,
                w.Word,
                w.Translation,
                w.Transcription,
                w.ContextPhrase,
                w.LanguageCode,
                w.IsMastered,
                w.RepetitionIntervalDays,
                w.NextReviewAt,
                isDueForReview = w.NextReviewAt <= DateTime.UtcNow
            }).ToList();

            return Results.Ok(new
            {
                totalWords = words.Count,
                masteredWords = words.Count(w => w.IsMastered),
                dueCount = words.Count(w => w.isDueForReview),
                words
            });
        });

        // 2. Добавление слова в личный словарь
        app.MapPost("/api/dictionary", (AddDictionaryWordRequest req, HttpContext ctx, LinguaDataStore store) =>
        {
            if (string.IsNullOrWhiteSpace(req.Word) || string.IsNullOrWhiteSpace(req.Translation))
            {
                return Results.BadRequest(new { error = "Укажите слово и перевод." });
            }

            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);
            string wordClean = req.Word.Trim();
            string lang = req.LanguageCode?.Trim().ToLowerInvariant() ?? user.Settings.LearningLanguage;

            // Проверяем, нет ли уже такого слова
            var existing = store.DictionaryWords.Values.FirstOrDefault(w =>
                w.UserId == user.Id &&
                w.Word.Equals(wordClean, StringComparison.OrdinalIgnoreCase) &&
                w.LanguageCode == lang);

            if (existing != null)
            {
                existing.Translation = req.Translation.Trim();
                if (!string.IsNullOrWhiteSpace(req.ContextPhrase)) existing.ContextPhrase = req.ContextPhrase.Trim();
                store.SaveToDisk();
                return Results.Ok(new { message = "Слово обновлено в словаре.", word = existing });
            }

            var newWord = new DictionaryWord
            {
                UserId = user.Id,
                Word = wordClean,
                Translation = req.Translation.Trim(),
                Transcription = req.Transcription?.Trim() ?? $"/{wordClean.ToLowerInvariant()}/",
                ContextPhrase = req.ContextPhrase?.Trim() ?? $"Example with '{wordClean}' in context.",
                LanguageCode = lang,
                AddedAt = DateTime.UtcNow,
                NextReviewAt = DateTime.UtcNow,
                RepetitionIntervalDays = 1,
                ConsecutiveCorrectAnswers = 0,
                IsMastered = false
            };

            store.DictionaryWords[newWord.Id] = newWord;
            user.DictionaryWords.Add(newWord);

            store.CheckAndAwardAchievements(user.Id);
            store.SaveToDisk();

            return Results.Ok(new { message = "Слово сохранено в словарь.", word = newWord });
        });

        // 3. Отправка результата интервального повторения карточки (SRS)
        app.MapPost("/api/dictionary/{id:guid}/review", (Guid id, WordReviewRequest req, LinguaDataStore store) =>
        {
            if (!store.DictionaryWords.TryGetValue(id, out var word))
            {
                return Results.NotFound(new { error = "Слово не найдено в словаре." });
            }

            string result = (req.Result ?? "remembered").ToLowerInvariant();
            if (result == "remembered" || result == "know")
            {
                word.ConsecutiveCorrectAnswers++;

                // Алгоритм SRS: 1 день -> 3 дня -> 7 дней -> 14 дней -> 30 дней
                word.RepetitionIntervalDays = word.ConsecutiveCorrectAnswers switch
                {
                    1 => 3,
                    2 => 7,
                    3 => 14,
                    _ => 30
                };

                // Статус "Изучено" присваивается ТОЛЬКО при отложенном повторении без подсказки (3+ успешных повторения)
                if (word.ConsecutiveCorrectAnswers >= 3)
                {
                    word.IsMastered = true;
                }

                word.NextReviewAt = DateTime.UtcNow.AddDays(word.RepetitionIntervalDays);
            }
            else
            {
                // При ошибке или затруднении сбрасываем счетчик повторений
                word.ConsecutiveCorrectAnswers = 0;
                word.RepetitionIntervalDays = 1;
                word.IsMastered = false;
                word.NextReviewAt = DateTime.UtcNow.AddHours(4);
            }

            store.SaveToDisk();

            return Results.Ok(new
            {
                message = "Результат повторения учтён.",
                word.Id,
                word.IsMastered,
                word.RepetitionIntervalDays,
                word.NextReviewAt
            });
        });

        // 4. Внешний переводчик и поиск по словарю (FreeDictionary / MyMemory / AI fallback)
        app.MapPost("/api/dictionary/translate", async (TranslateRequest req, HttpClient http) =>
        {
            if (string.IsNullOrWhiteSpace(req.Text))
            {
                return Results.BadRequest(new { error = "Введите слово для перевода." });
            }

            string text = req.Text.Trim();
            string from = req.FromLang ?? "en";
            string to = req.ToLang ?? "ru";

            var lookupResult = LookupBuiltinDictionary(text, from, to);
            if (lookupResult != null)
            {
                return Results.Ok(lookupResult);
            }

            // Попытка онлайн-запроса через публичный MyMemory API с таймаутом
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                string url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair={from}|{to}";
                var resp = await http.GetFromJsonAsync<MyMemoryResponse>(url, cts.Token);
                if (resp?.responseData?.translatedText != null)
                {
                    return Results.Ok(new
                    {
                        word = text,
                        translation = resp.responseData.translatedText,
                        transcription = $"/{text.ToLowerInvariant()}/",
                        contextPhrase = $"I noticed '{text}' during our conversation."
                    });
                }
            }
            catch { }

            // Базовый перевод по умолчанию
            return Results.Ok(new
            {
                word = text,
                translation = $"{text} (перевод)",
                transcription = $"/{text.ToLowerInvariant()}/",
                contextPhrase = $"Could you please explain '{text}'?"
            });
        });
    }

    private static object? LookupBuiltinDictionary(string text, string from, string to)
    {
        var dict = new Dictionary<string, (string tr, string phon, string ex)>(StringComparer.OrdinalIgnoreCase)
        {
            ["appointment"] = ("деловая встреча / приём", "/əˈpɔɪnt.mənt/", "I have an appointment with my dentist."),
            ["schedule"] = ("график / расписание", "/ˈskedʒ.uːl/", "Let's check our schedule for next week."),
            ["recommend"] = ("рекомендовать / советовать", "/ˌrek.əˈmend/", "Which coffee do you recommend?"),
            ["delicious"] = ("очень вкусный", "/dɪˈlɪʃ.əs/", "This croissant is really delicious!"),
            ["convenient"] = ("удобный / подходящий", "/kənˈviː.ni.ənt/", "Is Monday convenient for our meeting?"),
            ["reservation"] = ("бронирование", "/ˌrez.əˈveɪ.ʃən/", "I'd like to confirm my hotel reservation."),
            ["departure"] = ("отправление", "/dɪˈpɑːr.tʃər/", "The departure time is scheduled for 10:15."),
            ["destination"] = ("пункт назначения", "/ˌdes.tɪˈneɪ.ʃən/", "What is your final travel destination?"),
            ["experience"] = ("опыт / впечатление", "/ɪkˈspɪə.ri.əns/", "I have three years of experience in data analysis."),
            ["available"] = ("доступный / в наличии", "/əˈveɪ.lə.bəl/", "Is this jacket available in size medium?"),
            // Polish common phrases and words
            ["dzień dobry"] = ("добрый день / здравствуйте", "/dʑɛɲ ˈdɔbrɨ/", "Dzień dobry, poproszę jedną kawę."),
            ["dziękuję"] = ("спасибо / благодарю", "/dʑɛŋˈkujɛ/", "Dziękuję bardzo za pomoc!"),
            ["proszę"] = ("пожалуйста / прошу", "/ˈprɔ.ʂɛ/", "Proszę menu i rachunek."),
            ["kawa"] = ("кофе", "/ˈka.va/", "Czy macie kawę z mlekiem owsianym?"),
            ["herbata"] = ("чай", "/xɛrˈba.ta/", "Gorąca herbata z cytryną i miodem."),
            ["rachunek"] = ("счёт", "/raˈxu.nɛk/", "Czy mogę prosić o rachunek?"),
            ["rezerwacja"] = ("бронь / бронирование", "/rɛ.zɛrˈvat͡s.ja/", "Mam rezerwację pokoju na dzisiaj."),
            ["bilet"] = ("билет", "/ˈbi.lɛt/", "Poproszę jeden bilet do centrum."),
            ["stacja"] = ("станция / вокзал", "/ˈstat͡s.ja/", "Gdzie znajduje się najbliższa stacja metra?"),
            ["pokój"] = ("комната / гостиничный номер", "/ˈpɔ.kuj/", "Chciałbym zarezerwować pokój dwuosobowy."),
            ["spotkanie"] = ("встреча", "/spɔtˈka.ɲɛ/", "Nasze spotkanie projektowe zaczyna się o 14:00."),
            ["do widzenia"] = ("до свидания", "/dɔ viˈdzɛ.ɲa/", "Dziękuję za miłą rozmowę i do widzenia!")
        };

        if (dict.TryGetValue(text, out var val))
        {
            return new
            {
                word = text,
                translation = val.tr,
                transcription = val.phon,
                contextPhrase = val.ex
            };
        }

        return null;
    }

    private class MyMemoryResponse
    {
        public ResponseData? responseData { get; set; }
    }
    private class ResponseData
    {
        public string? translatedText { get; set; }
    }
}

public record AddDictionaryWordRequest(
    string Word,
    string Translation,
    string? Transcription,
    string? ContextPhrase,
    string? LanguageCode
);

public record WordReviewRequest(string? Result);
public record TranslateRequest(string Text, string? FromLang, string? ToLang);
