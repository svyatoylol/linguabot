
const API = {
    profile: "/api/profile",
    languages: "/api/profile/languages",
    dialogues: "/api/dialogues",
    placementTests: "/api/placement-tests"
};

const STORAGE_KEY = "linguabot_onboarding";

const languageNames = {
    en: "Английский",
    pl: "Польский",
    ru: "Русский"
};

const levelNames = {
    "A1-A2": "Начальный (A1–A2)",
    "B1-B2": "Средний (B1–B2)",
    "C1": "Продвинутый (C1)",
    "Unknown": "Не знаю"
};

const levelDescriptions = {
    "A1-A2": "Начальный уровень. Ты осваиваешь базовую лексику и простые конструкции.",
    "B1-B2": "Средний уровень. Ты уже можешь общаться на знакомые темы и выражать свои мысли.",
    "C1": "Продвинутый уровень. Ты уверенно используешь язык в сложных ситуациях."
};

const selectedLanguages = new Set();
const selectedLevels = {};

let profile = null;
let dialogueId = null;
let isSending = false;

let placementState = {
    languages: [],
    currentLanguageIndex: 0,
    testId: null,
    test: null,
    selectedAnswer: null,
    results: []
};

const statusElement = document.getElementById("status");
const input = document.getElementById("userInput");
const sendButton = document.getElementById("sendButton");
const form = document.getElementById("messageForm");
const messagesArea = document.getElementById("messages");

const screens = {
    languages: document.getElementById("languageScreen"),
    levels: document.getElementById("levelScreen"),
    choice: document.getElementById("testChoiceScreen"),
    placement: document.getElementById("placementScreen"),
    result: document.getElementById("resultScreen"),
    allResults: document.getElementById("allResultsScreen"),
    chat: messagesArea
};

function setStatus(text) {
    statusElement.textContent = text;
}

function showScreen(screenName) {
    Object.values(screens).forEach(screen => {
        screen.classList.add("hidden");
    });

    form.classList.add("hidden");

    screens[screenName].classList.remove("hidden");

    if (screenName === "chat") {
        form.classList.remove("hidden");
    }
}

async function apiRequest(url, options = {}) {
    const response = await fetch(url, {
        ...options,
        credentials: "same-origin",
        headers: {
            "Content-Type": "application/json",
            ...options.headers
        }
    });

    const text = await response.text();

    let data = {};

    try {
        data = text ? JSON.parse(text) : {};
    } catch {
        data = { message: text };
    }

    if (!response.ok) {
        throw new Error(
            data.detail ||
            data.message ||
            data.title ||
            `HTTP ${response.status}`
        );
    }

    return data;
}

function saveOnboardingState() {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({
        step: "placement",
        placementState
    }));
}

function loadOnboardingState() {
    try {
        const saved = localStorage.getItem(STORAGE_KEY);

        return saved ? JSON.parse(saved) : null;
    } catch {
        return null;
    }
}

function clearOnboardingState() {
    localStorage.removeItem(STORAGE_KEY);
}

// =====================================
// LANGUAGE SELECTION
// =====================================

document.querySelectorAll(".language-card").forEach(card => {
    card.addEventListener("click", () => {
        const language = card.dataset.language;

        if (selectedLanguages.has(language)) {
            selectedLanguages.delete(language);
        } else {
            selectedLanguages.add(language);
        }

        updateLanguageCards();
    });
});

function updateLanguageCards() {
    document.querySelectorAll(".language-card").forEach(card => {
        const selected = selectedLanguages.has(card.dataset.language);

        card.classList.toggle("selected", selected);

        card.querySelector(".language-check").textContent =
            selected ? "✓" : "";
    });

    document.getElementById("languagesNext").disabled =
        selectedLanguages.size === 0;
}

function renderLevelCards() {
    const container = document.getElementById("levelList");
    container.innerHTML = "";

    const levels = ["A1-A2", "B1-B2", "C1", "Unknown"];

    for (const language of selectedLanguages) {
        const card = document.createElement("section");
        card.className = "level-card";

        const title = document.createElement("h3");
        title.textContent = languageNames[language];

        const options = document.createElement("div");
        options.className = "level-options";

        for (const level of levels) {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "level-option";
            button.textContent = levelNames[level];

            if (selectedLevels[language] === level) {
                button.classList.add("selected");
            }

            button.addEventListener("click", () => {
                selectedLevels[language] = level;

                options.querySelectorAll("button").forEach(item => {
                    item.classList.remove("selected");
                });

                button.classList.add("selected");
                updateLevelButton();
            });

            options.appendChild(button);
        }

        const hint = document.createElement("div");
        hint.className = "level-hint";
        hint.textContent =
            "Не уверен? Выбери «Не знаю», и мы определим уровень тестированием.";

        card.append(title, options, hint);
        container.appendChild(card);
    }

    updateLevelButton();
}

function updateLevelButton() {
    const complete = [...selectedLanguages].every(language =>
        selectedLevels[language]
    );

    document.getElementById("levelsNext").disabled = !complete;
}

document.getElementById("languagesNext").addEventListener("click", () => {
    renderLevelCards();
    showScreen("levels");
    setStatus("Настройка языковых профилей");
});

document.getElementById("levelsBack").addEventListener("click", () => {
    showScreen("languages");
    setStatus("Выбор языков");
});

document.getElementById("levelsNext").addEventListener("click", async () => {
    const languages = [...selectedLanguages].map(language => ({
        languageCode: language,
        level: selectedLevels[language]
    }));

    try {
        setStatus("Сохранение языковых профилей...");

        await apiRequest(API.languages, {
            method: "PUT",
            body: JSON.stringify({ languages })
        });

        renderTestChoice();
        showScreen("choice");
        setStatus("Языковые профили сохранены");

    } catch (error) {
        setStatus("Ошибка сохранения");
        alert("Не удалось сохранить языковые профили: " + error.message);
    }
});

// =====================================
// TEST CHOICE
// =====================================

function renderTestChoice() {
    const container = document.getElementById("testLanguages");
    container.innerHTML = "";

    for (const language of selectedLanguages) {
        const item = document.createElement("div");
        item.className = "language-summary";

        item.textContent =
            languageNames[language] + " · " +
            levelNames[selectedLevels[language]];

        container.appendChild(item);
    }
}

document.getElementById("startTestButton").addEventListener("click", async () => {
    placementState = {
        languages: [...selectedLanguages],
        currentLanguageIndex: 0,
        testId: null,
        test: null,
        selectedAnswer: null,
        results: []
    };

    saveOnboardingState();
    await startCurrentLanguageTest();
});

document.getElementById("skipTestButton").addEventListener("click", async () => {
    clearOnboardingState();
    await startChat();
});

document.getElementById("exitTestButton").addEventListener("click", () => {
    showScreen("choice");
    setStatus("Тест сохранён. Его можно продолжить позже.");
});


// =====================================
// PLACEMENT TEST
// =====================================

async function startCurrentLanguageTest() {
    const language =
        placementState.languages[placementState.currentLanguageIndex];

    if (!language) {
        showAllResults();
        return;
    }

    try {
        setStatus(`Подготовка теста: ${languageNames[language]}...`);
        showScreen("placement");

        let test;

        if (placementState.testId) {
            test = await apiRequest(
                `${API.placementTests}/${placementState.testId}`,
                { method: "GET" }
            );
        } else {
            test = await apiRequest(API.placementTests, {
                method: "POST",
                body: JSON.stringify({
                    languageCode: language
                })
            });

            placementState.testId = test.id;
            saveOnboardingState();
        }

        if (test.status === "Completed") {
            throw new Error(
                "Этот тест уже завершён. Начни тестирование заново."
            );
        }

        placementState.test = test;
        placementState.selectedAnswer = null;

        saveOnboardingState();
        renderQuestion();

        setStatus(`Тестирование: ${languageNames[language]}`);

    } catch (error) {
        showTestError(error.message);
        setStatus("Ошибка тестирования");
    }
}

function renderQuestion() {
    const test = placementState.test;

    if (!test) return;

    const language =
        placementState.languages[placementState.currentLanguageIndex];

    const questions = test.questions || [];

    const question = questions.find(item => !item.isAnswered);

    const answeredCount = test.answeredCount ?? 0;
    const totalQuestions = test.totalQuestions ?? questions.length;

    document.getElementById("testLanguageLabel").textContent =
        `ТЕСТИРОВАНИЕ: ${languageNames[language].toUpperCase()}`;

    document.getElementById("progressText").textContent =
        `Вопрос ${Math.min(answeredCount + 1, totalQuestions)} из ${totalQuestions}`;

    document.getElementById("progressFill").style.width =
        `${(answeredCount / totalQuestions) * 100}%`;

    if (!question) {
        finishCurrentTest();
        return;
    }

    document.getElementById("questionTitle").textContent = question.prompt;
    document.getElementById("questionSkill").textContent =
        question.skill || "Выбери правильный ответ.";

    const optionsContainer = document.getElementById("answerOptions");
    optionsContainer.innerHTML = "";

    placementState.selectedAnswer = null;

    for (const option of question.options) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "answer-option";
        button.textContent = option;

        button.addEventListener("click", () => {
            placementState.selectedAnswer = option;

            optionsContainer.querySelectorAll("button").forEach(item => {
                item.classList.remove("selected");
            });

            button.classList.add("selected");

            document.getElementById("submitAnswerButton").disabled = false;
        });

        optionsContainer.appendChild(button);
    }

    document.getElementById("submitAnswerButton").disabled = true;
    document.getElementById("testError").classList.add("hidden");
}

document.getElementById("submitAnswerButton").addEventListener(
    "click",
    submitPlacementAnswer
);

async function submitPlacementAnswer() {
    const test = placementState.test;

    if (!test || !placementState.selectedAnswer) return;

    const question = test.questions.find(item => !item.isAnswered);

    if (!question) return;

    const button = document.getElementById("submitAnswerButton");
    button.disabled = true;
    button.textContent = "Сохранение...";

    try {
        await apiRequest(
            `${API.placementTests}/${placementState.testId}/answers`,
            {
                method: "POST",
                body: JSON.stringify({
                    questionId: question.id,
                    answer: placementState.selectedAnswer
                })
            }
        );

        // Получаем актуальное состояние теста с сервера.
        placementState.test = await apiRequest(
            `${API.placementTests}/${placementState.testId}`,
            { method: "GET" }
        );

        placementState.selectedAnswer = null;
        saveOnboardingState();

        renderQuestion();

    } catch (error) {
        showTestError(error.message);
        button.disabled = false;
    } finally {
        button.textContent = "Ответить";
    }
}

async function finishCurrentTest() {
    try {
        setStatus("Подсчёт результатов...");

        const result = await apiRequest(
            `${API.placementTests}/${placementState.testId}/finish`,
            { method: "POST" }
        );

        placementState.results.push({
            languageCode: result.languageCode,
            selectedLevel: result.selectedLevel,
            currentLevel: result.currentLevel,
            totalQuestions: result.totalQuestions,
            correctAnswers: result.correctAnswers,
            score: result.score,
            basicCorrect: result.basicCorrect,
            intermediateCorrect: result.intermediateCorrect,
            advancedCorrect: result.advancedCorrect
        });

        placementState.testId = null;
        placementState.test = null;
        placementState.selectedAnswer = null;

        saveOnboardingState();
        renderTestResult(result);

    } catch (error) {
        showTestError(error.message);
        setStatus("Не удалось завершить тест");
    }
}

function renderTestResult(result) {
    const language = languageNames[result.languageCode];

    document.getElementById("resultTitle").textContent =
        `Тест по языку: ${language}`;

    document.getElementById("resultLevel").textContent =
        result.currentLevel || "Не определён";

    document.getElementById("resultDescription").textContent =
        levelDescriptions[result.currentLevel] ||
        "Мы сохранили результат тестирования.";

    document.getElementById("resultCorrect").textContent =
        `${result.correctAnswers} из ${result.totalQuestions}`;

    document.getElementById("resultPercentage").textContent =
        `${result.score}%`;

    document.getElementById("resultProgress").style.width =
        `${result.score}%`;

    document.getElementById("resultDetails").innerHTML = `
        <div>Начальный уровень: ${result.basicCorrect}/4</div>
        <div>Средний уровень: ${result.intermediateCorrect}/4</div>
        <div>Продвинутый уровень: ${result.advancedCorrect}/4</div>
    `;

    showScreen("result");
    setStatus(`Тест по языку ${language} завершён`);
}

document.getElementById("continueAfterTestButton").addEventListener(
    "click",
    async () => {
        placementState.currentLanguageIndex++;

        saveOnboardingState();

        if (
            placementState.currentLanguageIndex <
            placementState.languages.length
        ) {
            await startCurrentLanguageTest();
        } else {
            showAllResults();
        }
    }
);

function showAllResults() {
    const container = document.getElementById("allResultsList");
    container.innerHTML = "";

    for (const result of placementState.results) {
        const item = document.createElement("div");
        item.className = "result-language";

        const title = document.createElement("strong");
        title.textContent = languageNames[result.languageCode];

        const level = document.createElement("div");
        level.textContent =
            `Уровень: ${result.currentLevel || "Не определён"}`;

        const score = document.createElement("div");
        score.textContent =
            `Правильных ответов: ${result.correctAnswers}/${result.totalQuestions} (${result.score}%)`;

        item.append(title, level, score);
        container.appendChild(item);
    }

    showScreen("allResults");
    setStatus("Все тесты завершены");
}

document.getElementById("startLearningButton").addEventListener(
    "click",
    async () => {
        clearOnboardingState();
        await startChat();
    }
);

function showTestError(message) {
    const element = document.getElementById("testError");
    element.textContent = message;
    element.classList.remove("hidden");
}


// =====================================
// CHAT
// =====================================

function addMessage(content, role = "assistant") {
    const element = document.createElement("div");

    element.className = "message " + role;
    element.textContent = content;

    messagesArea.appendChild(element);
    messagesArea.scrollTop = messagesArea.scrollHeight;

    return element;
}

async function startChat() {
    try {
        setStatus("Подготовка диалога...");
        showScreen("chat");

        input.disabled = true;
        sendButton.disabled = true;

        const savedDialogueId =
            localStorage.getItem("linguabot_dialogue_id");

        let dialogue = null;

        if (savedDialogueId) {
            try {
                const existing = await apiRequest(
                    `${API.dialogues}/${savedDialogueId}`,
                    { method: "GET" }
                );

                if (existing.status?.toLowerCase() === "active") {
                    dialogue = existing;
                }
            } catch {
                localStorage.removeItem("linguabot_dialogue_id");
            }
        }

        if (!dialogue) {
            dialogue = await apiRequest(API.dialogues, {
                method: "POST",
                body: JSON.stringify({
                    scenarioId: "introduction"
                })
            });

            localStorage.setItem(
                "linguabot_dialogue_id",
                dialogue.id
            );
        }

        dialogueId = dialogue.id;

        messagesArea.innerHTML = "";

        if (dialogue.messages) {
            for (const message of dialogue.messages) {
                const role =
                    message.role.toLowerCase() === "user"
                        ? "user"
                        : "assistant";

                addMessage(message.content, role);
            }
        }

        if (messagesArea.children.length === 0) {
            addMessage(
                "Привет! Давай практиковать язык. Напиши первое сообщение.",
                "assistant"
            );
        }

        setStatus("Диалог активен");
        input.disabled = false;
        sendButton.disabled = false;
        input.focus();

    } catch (error) {
        setStatus("Ошибка подключения");

        messagesArea.innerHTML = "";

        addMessage(
            "Не удалось инициализировать приложение: " + error.message,
            "error"
        );

        console.error(error);
    }
}

async function sendMessage() {
    const content = input.value.trim();

    if (!content || !dialogueId || isSending) return;

    isSending = true;
    input.disabled = true;
    sendButton.disabled = true;

    addMessage(content, "user");
    input.value = "";

    const loading = addMessage("LinguaBot печатает...", "system");

    try {
        const result = await apiRequest(
            `${API.dialogues}/${dialogueId}/messages`,
            {
                method: "POST",
                body: JSON.stringify({ content })
            }
        );

        loading.remove();

        addMessage(result.assistantMessage.content, "assistant");

    } catch (error) {
        loading.remove();

        addMessage(
            "Не удалось отправить сообщение: " + error.message,
            "error"
        );

        console.error(error);

    } finally {
        isSending = false;
        input.disabled = false;
        sendButton.disabled = false;
        input.focus();
    }
}

form.addEventListener("submit", event => {
    event.preventDefault();
    sendMessage();
});


// =====================================
// INITIALIZATION
// =====================================

async function initialize() {
    try {
        setStatus("Загрузка профиля...");

        profile = await apiRequest(API.profile, {
            method: "GET"
        });

        const savedLanguages = (profile.languages || [])
            .filter(language => language.isSelected);

        const savedState = loadOnboardingState();

        // Восстанавливаем незавершённое тестирование.
        if (
            savedState &&
            savedState.step === "placement" &&
            savedState.placementState
        ) {
            placementState = savedState.placementState;

            placementState.languages.forEach(language => {
                selectedLanguages.add(language);
            });

            placementState.languages.forEach(language => {
                const savedProfile = savedLanguages.find(
                    item => item.languageCode === language
                );

                if (savedProfile) {
                    selectedLevels[language] = savedProfile.selectedLevel;
                }
            });

            if (placementState.testId) {
                await startCurrentLanguageTest();
            } else if (
                placementState.currentLanguageIndex <
                placementState.languages.length
            ) {
                await startCurrentLanguageTest();
            } else {
                showAllResults();
            }

            return;
        }

        // Уже настроенный пользователь открывает чат.
        if (savedLanguages.length > 0) {
            savedLanguages.forEach(language => {
                selectedLanguages.add(language.languageCode);
                selectedLevels[language.languageCode] =
                    language.selectedLevel;
            });

            await startChat();
            return;
        }

        // Новый пользователь проходит onboarding.
        showScreen("languages");
        updateLanguageCards();

        setStatus("Добро пожаловать в LinguaBot");

    } catch (error) {
        setStatus("Ошибка подключения");
        console.error(error);

        alert(
            "Не удалось подключиться к серверу: " + error.message
        );
    }
}

initialize();
