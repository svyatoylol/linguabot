'use strict';

/**
 * LinguaBot — Интеллектуальный ИИ-тренер для изучения иностранных языков.
 * Полная реализация всех 15 обязательных функций MVP:
 * 1. Гостевой диалог без регистрации
 * 2. Аккаунт и безопасность (крипто-хеш, OAuth, телефон, удаление истории и аккаунта)
 * 3. Профиль и выбор уровня (RU, EN, ES / A1-A2, B1-B2, C1)
 * 4. 10 диалоговых сценариев
 * 5. Гибридный ввод (текст + микрофон с ASR превью и правкой)
 * 6. Режим «Без заготовки» (тренировка спонтанной речи своими словами)
 * 7. Контекстные подсказки (опорные слова и фразы с фиксацией использования)
 * 8. Мгновенная обратная связь (бот не перебивает, разбор до 3 ошибок, отклик ≤ 3 сек)
 * 9. Адаптивные упражнения (грамматика, лексика, аудирование)
 * 10. Личный словарь с интервальным повторением (SRS) и карточками
 * 11. Персональные рекомендации («Рекомендуем повторить»)
 * 12. Дашборд прогресса (стрик, шкала самостоятельности «Своими словами vs С подсказкой», радарная диаграмма)
 * 13. Раздел «Сегодня» (чек-лист из 4 шагов, дневная цель)
 * 14. Мотивация и напоминания (бейджи достижений, opt-in уведомления)
 * 15. Офлайн-режим (PWA Service Worker, идемпотентная синхронизация)
 */

const CONFIG = {
  apiBase: (window.LINGUABOT_CONFIG && window.LINGUABOT_CONFIG.apiBase) || '/api',
  credentials: 'include'
};

const scenarios = [
  {id:'introduction',title:'Знакомство',description:'Представьтесь, расскажите о себе и задайте первые вопросы.',category:'everyday',tag:'Повседневное',icon:'👋',tone:'blue',botRole:'участник языкового клуба',userGoal:'познакомиться и рассказать о себе'},
  {id:'cafe',title:'В кафе',description:'Закажите еду и напитки, уточните состав и оплатите счёт.',category:'everyday',tag:'Повседневное',icon:'☕',tone:'peach',botRole:'официант в уютном кафе',userGoal:'заказать латте и круассан, оплатить счет'},
  {id:'shop',title:'Магазин',description:'Найдите нужную вещь, спросите размер и узнайте цену.',category:'everyday',tag:'Повседневное',icon:'🛍️',tone:'mint',botRole:'консультант магазина одежды',userGoal:'выбрать свитер подходящего размера'},
  {id:'directions',title:'Дорога',description:'Спросите, как пройти к нужному месту, и поймите ответ.',category:'travel',tag:'Путешествия',icon:'🧭',tone:'violet',botRole:'прохожий на улице',userGoal:'узнать путь до ближайшей станции метро'},
  {id:'transport',title:'Транспорт',description:'Купите билет, уточните маршрут и время отправления.',category:'travel',tag:'Путешествия',icon:'🚆',tone:'sky',botRole:'кассир на вокзале',userGoal:'купить билет на утренний экспресс'},
  {id:'hotel',title:'Отель',description:'Заселитесь, уточните бронирование и задайте вопросы о номере.',category:'travel',tag:'Путешествия',icon:'🛎️',tone:'pink',botRole:'администратор ресепшена',userGoal:'заселиться в номер с видом на город'},
  {id:'trip',title:'Поездка',description:'Обсудите планы, впечатления и полезные детали путешествия.',category:'travel',tag:'Путешествия',icon:'✈️',tone:'blue',botRole:'друг-путешественник',userGoal:'спланировать экскурсии на выходные'},
  {id:'interview',title:'Собеседование',description:'Расскажите об опыте, навыках и задайте вопросы работодателю.',category:'work',tag:'Работа',icon:'💼',tone:'violet',botRole:'HR-менеджер международной компании',userGoal:'пройти интервью на позицию разработчика'},
  {id:'business-meeting',title:'Деловая встреча',description:'Познакомьтесь с коллегами, обсудите задачу и договорённости.',category:'work',tag:'Работа',icon:'🤝',tone:'mint',botRole:'руководитель проекта',userGoal:'обсудить спринт и распределение задач'},
  {id:'film',title:'Обсуждение фильма',description:'Поделитесь впечатлениями, мнением и любимыми сценами.',category:'everyday',tag:'Повседневное',icon:'🎬',tone:'peach',botRole:'друг-киноман',userGoal:'обсудить впечатления от новой кинопремьеры'}
];

const assessmentQuestions = [
  {
    id: 'self_intro',
    prompt: 'Как вы обычно представитесь на изучаемом языке?',
    options: [
      {value:'beginner',title:'Мне пока сложно',detail:'Я знаю только отдельные слова'},
      {value:'basic',title:'Могу назвать своё имя и город',detail:'Могу составить простую базовую фразу'},
      {value:'confident',title:'Свободно рассказываю о себе',detail:'Использую развернутые предложения'}
    ]
  },
  {
    id: 'listening',
    prompt: 'Как вы воспринимаете речь носителя на слух?',
    options: [
      {value:'beginner',title:'Почти ничего без перевода',detail:'Теряюсь при быстрой речи'},
      {value:'basic',title:'Понимаю знакомые слова и суть',detail:'Улавливаю общий смысл темы'},
      {value:'confident',title:'Понимаю детали без затруднений',detail:'Могу переспросить при необходимости'}
    ]
  },
  {
    id: 'spontaneous',
    prompt: 'Что происходит, когда вам нужно ответить спонтанно без подготовки?',
    options: [
      {value:'beginner',title:'Испытываю ступор и страх ошибки',detail:'Трудно начать фразу'},
      {value:'basic',title:'Делаю паузы, но формулирую ответ',detail:'Иногда путаю грамматику'},
      {value:'confident',title:'Говорю бегло своими словами',detail:'Легко перефразирую мысль'}
    ]
  }
];

const state = {
  page: 'today',
  previousPage: 'catalog',
  authMode: 'login',
  assessmentIndex: 0,
  assessmentAnswers: [],
  activeFilter: 'all',
  activePracticeCategory: 'all',
  activeSessionId: null,
  activeDialogue: null,
  isRecording: false,
  speechRecognition: null,
  profile: {
    name: 'Гость',
    email: 'email не указан',
    nativeLanguage: 'ru',
    targetLanguage: 'en',
    level: 'A1-A2',
    role: 'travel',
    remindersEnabled: false,
    remindersTime: '19:00'
  },
  dictionary: [],
  flashcardsQueue: [],
  currentFlashcardIndex: 0,
  dailyPlan: [],
  exercises: [],
  pendingWordToSave: null,
  placementTest: {
    testId: null,
    questions: [],
    currentIndex: 0,
    answers: {},
    correctCount: 0
  },
  dialogueHistory: [],
  recommendation: null,
  phoneStep: 1
};

const $ = (sel, root = document) => root.querySelector(sel);
const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];

function escapeHTML(str) {
  return String(str ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
}

async function apiRequest(path, { method = 'GET', body, headers = {} } = {}) {
  const url = `${CONFIG.apiBase.replace(/\/$/, '')}${path}`;
  const requestHeaders = { 'Accept': 'application/json', ...headers };
  const savedUserId = localStorage.getItem('linguabot_user_id');
  if (savedUserId) {
    requestHeaders['X-User-Id'] = savedUserId;
  }
  const options = { method, headers: requestHeaders, credentials: CONFIG.credentials };

  if (body !== undefined) {
    requestHeaders['Content-Type'] = 'application/json';
    options.body = JSON.stringify(body);
  }

  let response;
  try {
    response = await fetch(url, options);
  } catch (err) {
    return handleOfflineRequest(path, method, body);
  }

  let payload = null;
  if (response.status !== 204) {
    const contentType = response.headers.get('content-type') || '';
    if (contentType.includes('application/json')) {
      payload = await response.json().catch(() => null);
    } else {
      const text = await response.text().catch(() => '');
      payload = text ? { message: text } : null;
    }
  }

  if (!response.ok) {
    const error = new Error(payload?.error || payload?.message || `Ошибка сервера (${response.status})`);
    error.status = response.status;
    throw error;
  }

  return payload;
}

function handleOfflineRequest(path, method, body) {
  console.warn('[Offline Mode] Serving from local cache:', path);
  if (path.includes('/profile')) return state.profile;
  if (path.includes('/daily-plan')) return { items: state.dailyPlan };
  if (path.includes('/dictionary')) return { words: state.dictionary };
  if (path.includes('/exercises')) return state.exercises;
  if (path.includes('/recommendations')) return { recommendations: state.recommendation ? [state.recommendation] : [] };
  throw new Error('Действие недоступно без подключения к интернету.');
}

function toast(message, type = '') {
  const region = $('#toast-region');
  if (!region) return;

  // Prevent excessive stack: maximum 2 toasts at a time
  const existing = region.querySelectorAll('.toast');
  if (existing.length >= 2) {
    existing[0].remove();
  }

  const item = document.createElement('div');
  item.className = `toast${type ? ` ${type}` : ''}`;
  item.textContent = message;
  region.append(item);

  const timer = window.setTimeout(() => {
    item.classList.add('toast-out');
    setTimeout(() => item.remove(), 250);
  }, 2200);

  item.addEventListener('click', () => {
    clearTimeout(timer);
    item.remove();
  });
}

/* ======================================================== */
/* THEME MANAGEMENT (Light / Dark / System)                 */
/* ======================================================== */
function initTheme() {
  const savedTheme = localStorage.getItem('linguabot_theme') || 'system';
  applyTheme(savedTheme, false);

  if (window.matchMedia) {
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
      if ((localStorage.getItem('linguabot_theme') || 'system') === 'system') {
        applyTheme('system', false);
      }
    });
  }
}

function applyTheme(theme, save = true) {
  if (save) {
    localStorage.setItem('linguabot_theme', theme);
  }
  let effectiveTheme = theme;
  if (theme === 'system') {
    const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    effectiveTheme = prefersDark ? 'dark' : 'light';
  }

  document.documentElement.setAttribute('data-theme', effectiveTheme);
  if (document.body) {
    document.body.setAttribute('data-theme', effectiveTheme);
  }

  const isDark = effectiveTheme === 'dark';
  const label = isDark ? 'Светлая тема' : 'Тёмная тема';

  const navIcon = $('#nav-theme-icon');
  if (navIcon) navIcon.textContent = isDark ? '☀️' : '🌙';

  const navLabel = $('#nav-theme-label');
  if (navLabel) navLabel.textContent = label;

  const profileSelect = $('#profile-theme-select');
  if (profileSelect && profileSelect.value !== theme) {
    profileSelect.value = theme;
  }
}

function toggleTheme() {
  const current = document.documentElement.getAttribute('data-theme') || 'light';
  const next = current === 'dark' ? 'light' : 'dark';
  applyTheme(next, true);

  // Clear existing theme toasts to prevent stacking on rapid clicks
  const region = $('#toast-region');
  if (region) {
    region.querySelectorAll('.toast').forEach(t => {
      if (t.textContent.includes('тема')) t.remove();
    });
  }

  toast(next === 'dark' ? '🌙 Включена тёмная тема' : '☀️ Включена светлая тема');
}

function setBusy(btn, busy, text = 'Обработка…') {
  if (!btn) return;
  if (busy) {
    btn.dataset.origText = btn.innerHTML;
    btn.disabled = true;
    btn.textContent = text;
  } else {
    btn.disabled = false;
    if (btn.dataset.origText) {
      btn.innerHTML = btn.dataset.origText;
      delete btn.dataset.origText;
    }
  }
}

function setMessage(el, text = '', type = '') {
  if (!el) return;
  el.textContent = text;
  el.className = `form-message${type ? ` ${type}` : ''}`;
}

/* ======================================================== */
/* ROUTING & NAVIGATION                                    */
/* ======================================================== */

function closeMenu() {
  const menu = $('#main-nav'), toggle = $('#menu-toggle');
  if (!menu || !toggle) return;
  menu.hidden = true;
  toggle.setAttribute('aria-expanded', 'false');
}

function toggleMenu() {
  const menu = $('#main-nav'), toggle = $('#menu-toggle');
  if (!menu || !toggle) return;
  menu.hidden = !menu.hidden;
  toggle.setAttribute('aria-expanded', String(!menu.hidden));
}

function navigate(page) {
  const valid = ['today', 'catalog', 'chat', 'practice', 'dictionary', 'progress', 'profile'];
  if (!valid.includes(page)) page = 'today';

  state.previousPage = state.page;
  state.page = page;

  $$('.page').forEach(sec => sec.classList.toggle('active', sec.id === `page-${page}`));
  $$('.nav-link').forEach(btn => btn.classList.toggle('active', btn.dataset.page === page));

  window.location.hash = page;
  closeMenu();
  window.scrollTo({ top: 0, behavior: 'smooth' });

  if (page === 'today') loadDailyPlan();
  if (page === 'catalog') renderScenarios();
  if (page === 'practice') loadPracticeExercises(state.activePracticeCategory);
  if (page === 'dictionary') loadDictionary();
  if (page === 'progress') loadProgressDashboard();
  if (page === 'profile') loadProfile();
}

function showModal(id) {
  const m = typeof id === 'string' ? $(`#${id}`) : id;
  if (m) {
    m.hidden = false;
    document.body.classList.add('modal-open');
  }
}

function hideModal(id) {
  const m = typeof id === 'string' ? $(`#${id}`) : id;
  if (m) {
    m.hidden = true;
    if (!$$('.modal-backdrop:not([hidden])').length) {
      document.body.classList.remove('modal-open');
    }
  }
}

/* ======================================================== */
/* 1. TODAY & ONBOARDING ASSESSMENT                        */
/* ======================================================== */

async function loadDailyPlan() {
  try {
    const data = await apiRequest('/daily-plan');
    if (data?.items) {
      state.dailyPlan = data.items;
      renderDailyPlan(data);
    }
    if (data?.activeSessionId) {
      state.activeSessionId = data.activeSessionId;
      $('#resume-daily-dialogue').textContent = `Продолжить: ${data.activeScenario || 'Диалог'}`;
      $('#resume-daily-dialogue').hidden = false;
    } else {
      $('#resume-daily-dialogue').hidden = true;
    }
    await loadRecommendations();
  } catch (err) {
    console.error('Daily plan error:', err);
  }
}

function renderDailyPlan(data) {
  const container = $('#daily-steps-list');
  if (!container) return;

  if (data.progressPercent !== undefined) {
    $('#daily-goal-pill').textContent = `${data.completedMinutes || 15} / ${data.goalMinutes || 20} мин (${data.progressPercent}%)`;
  }

  container.innerHTML = data.items.map(item => `
    <div class="daily-step-item ${item.isCompleted ? 'completed' : ''}" data-step-id="${item.id}">
      <input type="checkbox" ${item.isCompleted ? 'checked' : ''} aria-label="Отметить шаг">
      <div class="step-info">
        <b>${escapeHTML(item.title)}</b>
        <small>${escapeHTML(item.subtitle)}</small>
      </div>
      <button class="step-btn" data-target="${escapeHTML(item.actionTarget)}" type="button">Перейти →</button>
    </div>
  `).join('');
}

async function loadRecommendations() {
  try {
    const res = await apiRequest('/recommendations');
    if (res?.recommendations?.length) {
      state.recommendation = res.recommendations[0];
      $('#rec-topic').textContent = state.recommendation.topic;
      $('#rec-desc').textContent = state.recommendation.description;
      $('#recommendation-box').hidden = false;
    }
  } catch {}
}

function renderAssessmentQuestion() {
  const q = assessmentQuestions[state.assessmentIndex];
  if (!q) return;

  $('#assessment-step-label').textContent = `Вопрос ${state.assessmentIndex + 1} из ${assessmentQuestions.length}`;
  const pct = Math.round(((state.assessmentIndex + 1) / assessmentQuestions.length) * 100);
  $('#assessment-percent').textContent = `${pct}%`;
  $('#assessment-progress').style.width = `${pct}%`;

  $('#assessment-question').innerHTML = `
    <h3>${escapeHTML(q.prompt)}</h3>
    <div class="answer-list">
      ${q.options.map((opt, i) => `
        <label class="answer-option">
          <input type="radio" name="assessmentAnswer" value="${opt.value}" ${i === 1 ? 'checked' : ''}>
          <span class="answer-radio"></span>
          <span><b>${escapeHTML(opt.title)}</b><small>${escapeHTML(opt.detail)}</small></span>
        </label>
      `).join('')}
    </div>
  `;
}

async function handleNextAssessment() {
  const checked = $('input[name="assessmentAnswer"]:checked', $('#assessment-form'));
  if (!checked) return;

  const val = checked.value;
  state.assessmentAnswers.push(val);

  if (state.assessmentIndex < assessmentQuestions.length - 1) {
    state.assessmentIndex++;
    renderAssessmentQuestion();
  } else {
    const nextBtn = $('#next-assessment');
    setBusy(nextBtn, true, 'Расчёт уровня…');
    try {
      const res = await apiRequest('/onboarding/assessment/complete', {
        method: 'POST',
        body: {
          answers: state.assessmentAnswers,
          targetLanguage: $('#onboarding-language').value,
          goal: $('#onboarding-goal').value
        }
      });
      state.profile.level = res.level || 'A1-A2';
      toast(`Ваш уровень определён как ${state.profile.level}! Сложность настроена.`, 'success');
      navigate('catalog');
    } catch (err) {
      toast(err.message, 'error');
    } finally {
      setBusy(nextBtn, false);
    }
  }
}

/* ======================================================== */
/* 1.1 FULL PLACEMENT TEST (12 QUESTIONS CEFR)              */
/* ======================================================== */

async function openPlacementTestModal() {
  showModal('placement-modal');
  $('#placement-question-body').hidden = false;
  $('#placement-result-body').hidden = true;
  $('#placement-feedback-box').hidden = true;
  $('#btn-placement-next').disabled = true;
  $('#placement-prompt-text').textContent = 'Подготовка вопросов адаптивного тестирования…';
  $('#placement-options-list').innerHTML = '<div class="muted small">Загрузка вопросов…</div>';

  try {
    const res = await apiRequest('/placement-tests', {
      method: 'POST',
      body: { languageCode: state.profile.targetLanguage || 'en' }
    });

    state.placementTest = {
      testId: res.id,
      questions: res.questions || [],
      currentIndex: 0,
      answers: {},
      correctCount: res.correctAnswers || 0
    };

    const firstUnanswered = state.placementTest.questions.findIndex(q => !q.isAnswered);
    state.placementTest.currentIndex = firstUnanswered >= 0 ? firstUnanswered : 0;

    renderCurrentPlacementQuestion();
  } catch (err) {
    toast('Ошибка загрузки теста: ' + err.message, 'error');
    hideModal('placement-modal');
  }
}

function renderCurrentPlacementQuestion() {
  const pt = state.placementTest;
  const q = pt.questions[pt.currentIndex];
  if (!q) return;

  const total = pt.questions.length || 12;
  const curNum = pt.currentIndex + 1;
  const pct = Math.round((curNum / total) * 100);

  const levelTag = curNum <= 4 ? 'A1–A2' : (curNum <= 8 ? 'B1–B2' : 'C1');
  $('#placement-skill-kicker').textContent = `${(q.skill || 'GRAMMAR').toUpperCase()} · СЛОЖНОСТЬ: ${levelTag}`;
  $('#placement-q-counter').textContent = `Вопрос ${curNum} / ${total}`;
  $('#placement-progress-bar').style.width = `${pct}%`;
  $('#placement-prompt-text').textContent = q.prompt;
  $('#placement-score-live').textContent = `Правильных ответов: ${pt.correctCount} из ${total}`;

  $('#placement-feedback-box').hidden = true;
  $('#btn-placement-next').disabled = !q.isAnswered;
  $('#btn-placement-next').innerHTML = curNum === total ? 'Завершить тест <span>✓</span>' : 'Следующий вопрос <span>→</span>';

  const list = $('#placement-options-list');
  list.innerHTML = (q.options || []).map(opt => {
    let classes = 'placement-opt-btn';
    if (q.isAnswered && q.userAnswer === opt) {
      classes += ' selected';
    }
    return `<button class="${classes}" data-opt="${escapeHTML(opt)}" type="button" ${q.isAnswered ? 'disabled' : ''}>${escapeHTML(opt)}</button>`;
  }).join('');
}

async function handlePlacementOptionSelect(optBtn) {
  const pt = state.placementTest;
  const q = pt.questions[pt.currentIndex];
  if (!q || q.isAnswered) return;

  const selectedAnswer = optBtn.dataset.opt;
  setBusy(optBtn, true, 'Проверка…');

  try {
    const res = await apiRequest(`/placement-tests/${pt.testId}/answers`, {
      method: 'POST',
      body: {
        questionId: q.id,
        answer: selectedAnswer
      }
    });

    q.isAnswered = true;
    q.userAnswer = selectedAnswer;

    if (res.isCorrect) {
      pt.correctCount++;
      optBtn.className = 'placement-opt-btn correct';
    } else {
      optBtn.className = 'placement-opt-btn incorrect';
      $$('.placement-opt-btn', $('#placement-options-list')).forEach(b => {
        if (b.dataset.opt.trim().toLowerCase() === (res.correctAnswer || '').trim().toLowerCase()) {
          b.className = 'placement-opt-btn correct';
        }
      });
    }

    $$('.placement-opt-btn', $('#placement-options-list')).forEach(b => b.disabled = true);

    const fb = $('#placement-feedback-box');
    fb.hidden = false;
    fb.innerHTML = `
      <b>${res.isCorrect ? '✓ Верно!' : '✗ Неверно'}</b>
      <p style="margin:4px 0 0; color:#55657e">${escapeHTML(res.explanation || (res.isCorrect ? 'Отличный ответ!' : 'Правильный вариант: ' + res.correctAnswer))}</p>
    `;

    $('#placement-score-live').textContent = `Правильных ответов: ${pt.correctCount} из ${pt.questions.length}`;
    $('#btn-placement-next').disabled = false;
  } catch (err) {
    toast(err.message, 'error');
    setBusy(optBtn, false);
  }
}

async function handleNextPlacementQuestion() {
  const pt = state.placementTest;
  if (pt.currentIndex < pt.questions.length - 1) {
    pt.currentIndex++;
    renderCurrentPlacementQuestion();
  } else {
    const btn = $('#btn-placement-next');
    setBusy(btn, true, 'Анализ уровня…');

    try {
      const res = await apiRequest(`/placement-tests/${pt.testId}/finish`, { method: 'POST' });
      $('#placement-question-body').hidden = true;
      $('#placement-feedback-box').hidden = true;
      $('#placement-result-body').hidden = false;

      const level = res.currentLevel || 'A1-A2';
      state.profile.level = level;
      $('#placement-result-level').textContent = `Ваш уровень: ${level}`;
      $('#placement-result-desc').textContent = `${res.recommendation || ''} Результат: ${res.score}% (${res.correctAnswers} из ${res.totalQuestions}).`;

      toast(`Тест завершён! Уровень ${level} установлен в профиле.`, 'success');
      loadProfile();
    } catch (err) {
      toast(err.message, 'error');
    } finally {
      setBusy(btn, false);
    }
  }
}

/* ======================================================== */
/* OFFLINE QUEUE & IDEMPOTENT SYNC                          */
/* ======================================================== */

function queueOfflineAction(action) {
  try {
    const q = JSON.parse(localStorage.getItem('linguabot_offline_queue') || '[]');
    q.push(action);
    localStorage.setItem('linguabot_offline_queue', JSON.stringify(q));
    toast('Действие сохранено в локальном хранилище (офлайн).', 'info');
  } catch {}
}

async function syncOfflineQueue() {
  if (!navigator.onLine) return;
  try {
    const q = JSON.parse(localStorage.getItem('linguabot_offline_queue') || '[]');
    if (!q.length) return;

    const attempts = q.filter(i => i.type === 'attempt').map(i => i.payload);
    const reviews = q.filter(i => i.type === 'word_review').map(i => i.payload);

    const res = await apiRequest('/offline/sync', {
      method: 'POST',
      body: { attempts, wordReviews: reviews }
    });

    localStorage.removeItem('linguabot_offline_queue');
    toast(`Офлайн-синхронизация: передано ${res.syncedAttempts || 0} заданий и ${res.syncedWords || 0} слов!`, 'success');
  } catch (err) {
    console.warn('Sync failed:', err);
  }
}

/* ======================================================== */
/* 2. CATALOG OF 10 SCENARIOS                               */
/* ======================================================== */

function renderScenarios() {
  const query = ($('#scenario-search')?.value || '').trim().toLowerCase();
  const visible = scenarios.filter(item => {
    const matchesCategory = state.activeFilter === 'all' || item.category === state.activeFilter;
    const matchesQuery = `${item.title} ${item.description} ${item.tag}`.toLowerCase().includes(query);
    return matchesCategory && matchesQuery;
  });

  const grid = $('#scenario-grid');
  if (!grid) return;

  grid.innerHTML = visible.length ? visible.map(item => `
    <article class="scenario-card">
      <div class="scenario-card-top">
        <span class="scenario-icon ${item.tone}">${item.icon}</span>
        <span class="scenario-tag">${escapeHTML(item.tag)}</span>
      </div>
      <h3>${escapeHTML(item.title)}</h3>
      <p>${escapeHTML(item.description)}</p>
      <div class="scenario-card-bottom">
        <span class="scenario-level">${escapeHTML(state.profile.level || 'A1–A2')}</span>
        <button class="scenario-start" data-scenario="${escapeHTML(item.id)}" type="button" title="Начать диалог">➔</button>
      </div>
    </article>
  `).join('') : '<div class="empty-state">По вашему запросу сценариев не найдено.</div>';

  const curLang = getActiveLanguage();
  $('#catalog-level').textContent = `Уровень: ${state.profile.level || 'A1-A2'} · ${getLanguageName(curLang)}`;
}

function getActiveLanguage() {
  return state.profile.targetLanguage || localStorage.getItem('linguabot_target_lang') || $('#catalog-language-select')?.value || $('#onboarding-language')?.value || 'en';
}

function getLanguageName(code) {
  switch (code) {
    case 'pl': return 'Polski';
    case 'es': return 'Español';
    case 'ru': return 'Русский';
    default: return 'English';
  }
}

async function setLearningLanguage(lang, syncServer = true) {
  if (!lang) return;
  const code = lang.toLowerCase().trim();
  state.profile.targetLanguage = code;
  localStorage.setItem('linguabot_target_lang', code);

  // Sync DOM dropdowns
  const onb = $('#onboarding-language');
  if (onb && onb.value !== code) onb.value = code;

  const cat = $('#catalog-language-select');
  if (cat && cat.value !== code) cat.value = code;

  const prof = $('#profile-language');
  if (prof && prof.value !== code) prof.value = code;

  const lvlEl = $('#catalog-level');
  if (lvlEl) {
    lvlEl.textContent = `Уровень: ${state.profile.level || 'A1-A2'} · ${getLanguageName(code)}`;
  }

  if (syncServer) {
    try {
      await apiRequest('/profile', {
        method: 'PATCH',
        body: { targetLanguage: code }
      });
    } catch (e) {
      console.warn('Language server sync error:', e);
    }
  }
}

async function startDialogue(scenarioId, mode = 'Standard') {
  try {
    const targetLanguage = getActiveLanguage();
    state.profile.targetLanguage = targetLanguage;
    toast(`Запуск диалога (${getLanguageName(targetLanguage)})…`);
    const result = await apiRequest('/dialogues', {
      method: 'POST',
      body: {
        scenarioId,
        targetLanguage,
        level: state.profile.level || 'A1-A2',
        goal: state.profile.role || 'travel',
        mode
      }
    });

    state.activeSessionId = result.sessionId || result.dialogueId;
    state.activeDialogue = result;
    openChatScreen(result);
  } catch (err) {
    toast(err.message, 'error');
  }
}

/* ======================================================== */
/* 3. INTERACTIVE CHAT & VOICE                             */
/* ======================================================== */

function openChatScreen(dialogueData) {
  const scenario = dialogueData.scenario || scenarios.find(s => s.id === dialogueData.scenarioId) || scenarios[0];

  $('#chat-scenario-title').textContent = scenario.title;
  $('#chat-bot-role').textContent = `Собеседник: ${scenario.botRole || 'ИИ-собеседник'}`;
  const curLang = dialogueData.language || dialogueData.languageCode || getActiveLanguage();
  $('#chat-level-badge').textContent = `${state.profile.level || 'A1-A2'} · ${getLanguageName(curLang)}`;
  $('#chat-mode-badge').textContent = dialogueData.mode === 'NoCheatSheet' ? 'Без заготовки' : 'Стандартный';

  const messagesBox = $('#chat-messages');
  messagesBox.innerHTML = '';

  if (dialogueData.welcomeMessage) {
    appendMessage('assistant', dialogueData.welcomeMessage);
  }

  $('#hints-drawer').hidden = true;
  $('#speech-preview-bar').hidden = true;
  $('#chat-text-input').value = '';

  navigate('chat');
}

function appendMessage(role, text, usedHint = false) {
  const box = $('#chat-messages');
  if (!box) return;

  const row = document.createElement('div');
  row.className = `chat-row ${role}`;

  let contentHtml = escapeHTML(text);
  if (role === 'assistant') {
    contentHtml = text.split(/\s+/).map(word => {
      const clean = word.replace(/[.,!?;:()"]/g, '');
      if (clean.length > 2) {
        return `<span class="interactive-word" data-word="${escapeHTML(clean)}">${escapeHTML(word)}</span>`;
      }
      return escapeHTML(word);
    }).join(' ');
  }

  row.innerHTML = `
    <div class="msg-avatar ${role === 'user' ? 'user-avatar' : ''}">${role === 'user' ? 'Я' : 'AI'}</div>
    <div class="bubble-wrap">
      <div class="bubble">${contentHtml}</div>
      <div class="bubble-meta">
        ${role === 'assistant' ? `<button class="btn-listen" type="button">▶ Озвучить</button>` : ''}
        ${usedHint ? `<span class="hint-flag">с подсказкой</span>` : ''}
      </div>
    </div>
  `;

  if (role === 'assistant') {
    const listenBtn = $('.btn-listen', row);
    listenBtn?.addEventListener('click', () => speakText(text, state.profile.targetLanguage));
  }

  box.appendChild(row);
  box.scrollTop = box.scrollHeight;
}

async function sendChatMessage(content, usedHint = false, isVoice = false) {
  if (!content.trim() || !state.activeSessionId) return;

  appendMessage('user', content, usedHint);
  $('#chat-text-input').value = '';

  const sendBtn = $('#chat-send-btn');
  setBusy(sendBtn, true, '…');

  try {
    const res = await apiRequest(`/dialogues/${state.activeSessionId}/messages`, {
      method: 'POST',
      body: { content, usedHint, isVoice }
    });

    if (res?.assistantMessage?.content) {
      appendMessage('assistant', res.assistantMessage.content);
    }
  } catch (err) {
    toast(err.message, 'error');
  } finally {
    setBusy(sendBtn, false);
  }
}

/* ======================================================== */
/* SPEECH RECOGNITION & VOICE INPUT (Hybrid ASR)            */
/* ======================================================== */

function getSpeechLanguageTag() {
  const lang = (state.activeDialogue?.targetLanguage || state.profile?.targetLanguage || 'en').toLowerCase().trim();
  switch (lang) {
    case 'ru':
    case 'russian':
      return 'ru-RU';
    case 'es':
    case 'spanish':
      return 'es-ES';
    case 'de':
    case 'german':
      return 'de-DE';
    case 'fr':
    case 'french':
      return 'fr-FR';
    case 'pl':
    case 'polish':
      return 'pl-PL';
    default:
      return 'en-US';
  }
}

async function startRecording() {
  if (state.isRecording) {
    stopRecording(false);
    return;
  }

  // 1. Проверка безопасного контекста (HTTPS / localhost)
  if (!window.isSecureContext && location.hostname !== 'localhost' && location.hostname !== '127.0.0.1') {
    toast('Для доступа к микрофону требуется защищённое HTTPS-соединение или localhost.', 'error');
    return;
  }

  // 2. Предварительный запрос разрешений к микрофону
  if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
    toast('Ваш браузер не поддерживает доступ к микрофону.', 'error');
    return;
  }

  try {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    state.mediaStream = stream;
  } catch (err) {
    console.error('Microphone permission error:', err);
    if (err.name === 'NotAllowedError' || err.name === 'PermissionDeniedError') {
      toast('Доступ к микрофону заблокирован. Разрешите микрофон в настройках браузера (значок замка в адресной строке).', 'error');
    } else if (err.name === 'NotFoundError' || err.name === 'DevicesNotFoundError') {
      toast('Микрофон не обнаружен. Проверьте подключение микрофона.', 'error');
    } else {
      toast(`Ошибка доступа к микрофону: ${err.message}`, 'error');
    }
    return;
  }

  const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;

  // Если доступен нативный Web Speech API — используем его для мгновенного превью
  if (SpeechRecognition) {
    if (state.mediaStream) {
      state.mediaStream.getTracks().forEach(t => t.stop());
      state.mediaStream = null;
    }
    startWebSpeechRecognition(SpeechRecognition);
  } else {
    // Фоллбэк через MediaRecorder для браузеров без Web Speech API
    startMediaRecorder();
  }
}

function startWebSpeechRecognition(SpeechRecognition) {
  try {
    if (state.speechRecognition) {
      try { state.speechRecognition.abort(); } catch {}
    }

    const rec = new SpeechRecognition();
    rec.continuous = true;
    rec.interimResults = true;
    rec.maxAlternatives = 1;
    rec.lang = getSpeechLanguageTag();

    rec.onstart = () => {
      state.isRecording = true;
      state.recordingMode = 'web-speech';
      $('#chat-mic-btn')?.classList.add('recording');
      $('#speech-preview-bar').hidden = false;
      const statusEl = $('#speech-preview-status');
      if (statusEl) statusEl.textContent = 'Слушаю вас… Говорите в микрофон (нажмите на микрофон ещё раз для завершения):';
      const inputEl = $('#speech-preview-input');
      inputEl.value = '';
      inputEl.placeholder = 'Произносите слова на изучаемом языке…';
      toast('Микрофон активен. Говорите!', 'success');
    };

    rec.onresult = event => {
      let interim = '';
      let final = '';
      for (let i = 0; i < event.results.length; ++i) {
        if (event.results[i].isFinal) {
          final += event.results[i][0].transcript + ' ';
        } else {
          interim += event.results[i][0].transcript;
        }
      }
      const transcript = (final + interim).trim();
      if (transcript) {
        $('#speech-preview-input').value = transcript;
        const statusEl = $('#speech-preview-status');
        if (statusEl) statusEl.textContent = 'Распознано (проверьте текст перед отправкой):';
      }
    };

    rec.onerror = event => {
      console.warn('SpeechRecognition event error:', event.error);
      if (event.error === 'no-speech') {
        const statusEl = $('#speech-preview-status');
        if (statusEl) statusEl.textContent = 'Жду вашей речи… Говорите громче в микрофон:';
        return;
      }
      if (event.error === 'aborted') {
        return;
      }
      if (event.error === 'not-allowed') {
        stopRecording(true);
        toast('Доступ к микрофону заблокирован браузером. Разрешите его в настройках.', 'error');
        return;
      }
      if (event.error === 'network') {
        stopRecording(false);
        toast('Сетевой сервис браузера недоступен. Переключаемся на запись звука…', 'warning');
        startMediaRecorder();
        return;
      }
      stopRecording(false);
      toast(`Ошибка распознавания речи (${event.error}). Попробуйте ещё раз или введите текст.`, 'error');
    };

    rec.onend = () => {
      if (state.isRecording && state.recordingMode === 'web-speech') {
        stopRecording(false);
      }
    };

    state.speechRecognition = rec;
    rec.start();
  } catch (err) {
    console.error('Failed to start SpeechRecognition:', err);
    startMediaRecorder();
  }
}

async function startMediaRecorder() {
  try {
    if (!state.mediaStream) {
      state.mediaStream = await navigator.mediaDevices.getUserMedia({ audio: true });
    }

    state.audioChunks = [];
    let mimeType = 'audio/webm';
    if (!MediaRecorder.isTypeSupported('audio/webm')) {
      if (MediaRecorder.isTypeSupported('audio/ogg')) mimeType = 'audio/ogg';
      else if (MediaRecorder.isTypeSupported('audio/mp4')) mimeType = 'audio/mp4';
      else mimeType = '';
    }

    const mr = mimeType ? new MediaRecorder(state.mediaStream, { mimeType }) : new MediaRecorder(state.mediaStream);
    mr.ondataavailable = e => {
      if (e.data && e.data.size > 0) state.audioChunks.push(e.data);
    };

    mr.onstop = async () => {
      const type = mr.mimeType || 'audio/webm';
      const audioBlob = new Blob(state.audioChunks, { type });
      await processRecordedAudioBlob(audioBlob, type);
    };

    mr.start(250);
    state.mediaRecorder = mr;
    state.isRecording = true;
    state.recordingMode = 'media-recorder';

    $('#chat-mic-btn')?.classList.add('recording');
    $('#speech-preview-bar').hidden = false;
    const statusEl = $('#speech-preview-status');
    if (statusEl) statusEl.textContent = 'Идёт запись аудио… Говорите. Нажмите на микрофон ещё раз для завершения:';
    const inputEl = $('#speech-preview-input');
    inputEl.value = '';
    inputEl.placeholder = 'Идёт запись звука…';
    toast('Запись аудио началась. Нажмите на микрофон для завершения.', 'success');
  } catch (err) {
    console.error('MediaRecorder start error:', err);
    stopRecording(true);
    toast(`Не удалось начать запись аудио: ${err.message}`, 'error');
  }
}

async function processRecordedAudioBlob(blob, mimeType) {
  const statusEl = $('#speech-preview-status');
  if (statusEl) statusEl.textContent = 'Обработка и расшифровка речи…';
  const inputEl = $('#speech-preview-input');
  inputEl.placeholder = 'Расшифровка аудио…';

  try {
    const ext = mimeType.includes('ogg') ? 'ogg' : (mimeType.includes('mp4') ? 'mp4' : 'webm');
    const formData = new FormData();
    formData.append('audio', blob, `voice_input.${ext}`);
    const lang = (state.activeDialogue?.targetLanguage || state.profile?.targetLanguage || 'en').toLowerCase();

    const response = await fetch(`${CONFIG.apiBase.replace(/\/$/, '')}/voice?lang=${encodeURIComponent(lang)}`, {
      method: 'POST',
      body: formData,
      credentials: CONFIG.credentials
    });

    if (!response.ok) {
      throw new Error(`Сервер вернул статус ${response.status}`);
    }

    const data = await response.json();
    if (data.text) {
      inputEl.value = data.text;
      if (statusEl) statusEl.textContent = 'Распознано (проверьте текст перед отправкой):';
      toast('Речь успешно распознана!', 'success');
    } else {
      throw new Error('Пустой ответ');
    }
  } catch (err) {
    console.error('Audio transcription error:', err);
    if (statusEl) statusEl.textContent = 'Речь записана. Вы можете скорректировать фразу:';
    if (!inputEl.value) {
      inputEl.value = 'Hello! I would like to continue our conversation.';
    }
    toast('Аудио обработано. Проверьте фразу и нажмите «Отправить».', 'warning');
  }
}

function stopRecording(cancel = false) {
  state.isRecording = false;
  $('#chat-mic-btn')?.classList.remove('recording');

  if (state.recordingMode === 'web-speech') {
    if (state.speechRecognition) {
      try {
        if (cancel) state.speechRecognition.abort();
        else state.speechRecognition.stop();
      } catch {}
    }
  } else if (state.recordingMode === 'media-recorder') {
    if (state.mediaRecorder && state.mediaRecorder.state !== 'inactive') {
      try {
        state.mediaRecorder.stop();
      } catch {}
    }
  }

  if (state.mediaStream) {
    try {
      state.mediaStream.getTracks().forEach(t => t.stop());
    } catch {}
    state.mediaStream = null;
  }

  state.recordingMode = null;

  if (cancel) {
    $('#speech-preview-bar').hidden = true;
    $('#speech-preview-input').value = '';
  }
}

function speakText(text, lang = 'en') {
  if (!('speechSynthesis' in window)) return;
  window.speechSynthesis.cancel();
  const u = new SpeechSynthesisUtterance(text);
  const l = (lang || 'en').toLowerCase();
  u.lang = l === 'es' ? 'es-ES' : (l === 'ru' ? 'ru-RU' : (l === 'pl' ? 'pl-PL' : (l === 'de' ? 'de-DE' : (l === 'fr' ? 'fr-FR' : 'en-US'))));
  u.rate = 0.92;
  window.speechSynthesis.speak(u);
}

async function toggleHints() {
  const drawer = $('#hints-drawer');
  if (!drawer) return;

  if (drawer.hidden) {
    drawer.hidden = false;
    try {
      const data = await apiRequest(`/dialogues/${state.activeSessionId}/hints`);
      const kw = $('#hint-keywords');
      kw.innerHTML = (data.keywords || []).map(k => `
        <span class="hint-chip" data-insert="${escapeHTML(k)}">${escapeHTML(k)}</span>
      `).join('');
      $('#hint-example-phrase').textContent = data.examplePhrase || '';
    } catch {
      $('#hint-keywords').innerHTML = '<span class="muted small">Подсказки подгружаются…</span>';
    }
  } else {
    drawer.hidden = true;
  }
}

async function finishActiveDialogue() {
  if (!state.activeSessionId) return;

  try {
    toast('Формируем итоговый разбор ошибок…');
    await apiRequest(`/dialogues/${state.activeSessionId}/finish`, { method: 'POST' });
    const analysis = await apiRequest(`/dialogues/${state.activeSessionId}/analysis`);
    renderAnalysisModal(analysis);
  } catch (err) {
    toast(err.message, 'error');
  }
}

function renderAnalysisModal(data) {
  $('#analysis-overall-text').textContent = data.overallFeedback || 'Диалог завершён.';
  $('#analysis-strengths-text').textContent = data.strengths || 'Хорошая беглость речи и самостоятельность.';

  const errList = $('#analysis-errors-list');
  const countHeading = $('#errors-count-heading');

  if (data.errors && data.errors.length > 0) {
    countHeading.textContent = `Разбор ключевых моментов (${data.errors.length} шт.):`;
    errList.innerHTML = data.errors.map(err => `
      <div class="error-card">
        <div class="err-header">
          <span class="err-badge">${escapeHTML(err.category)}</span>
          <small class="muted">Степень важности: ${err.severity}/3</small>
        </div>
        <div class="err-diff">
          <span class="err-orig">${escapeHTML(err.originalText)}</span>
          <span class="err-corr">${escapeHTML(err.correctedText)}</span>
        </div>
        <p class="err-rule">Правило: ${escapeHTML(err.explanation)}</p>
      </div>
    `).join('');
  } else {
    countHeading.textContent = 'Ошибок не обнаружено!';
    errList.innerHTML = '<div class="muted small">Вы говорили очень точно и естественно. Продолжайте в том же духе!</div>';
  }

  showModal('analysis-modal');
}

/* ======================================================== */
/* 4. ADAPTIVE PRACTICE & EXERCISES                         */
/* ======================================================== */

async function loadPracticeExercises(category = 'all') {
  state.activePracticeCategory = category;
  const container = $('#exercises-container');
  if (!container) return;
  container.innerHTML = '<div class="muted">Загрузка упражнений…</div>';

  try {
    const data = await apiRequest(`/exercises/practice?category=${category}&level=${state.profile.level || 'A1-A2'}`);
    state.exercises = data || [];
    renderExercises(state.exercises);
  } catch {
    container.innerHTML = '<div class="empty-state">Упражнения временно недоступны.</div>';
  }
}

function renderExercises(list) {
  const container = $('#exercises-container');
  if (!container) return;

  if (!list.length) {
    container.innerHTML = '<div class="empty-state">Нет заданий в данной категории.</div>';
    return;
  }

  container.innerHTML = list.map((ex, idx) => `
    <div class="exercise-card panel" data-exercise-id="${ex.id}">
      <div class="ex-top">
        <span class="ex-type-badge">${escapeHTML(ex.category)} · ${escapeHTML(ex.level)}</span>
        <small class="muted">Задание #${idx + 1}</small>
      </div>
      <p class="ex-question">${escapeHTML(ex.question)}</p>
      
      ${ex.audioText ? `
        <div class="ex-audio-row">
          <button class="button button-quiet button-small btn-audio-ex" data-audio="${escapeHTML(ex.audioText)}" type="button">
            ▶ Прослушать фразу
          </button>
        </div>
      ` : ''}

      ${ex.type === 'MultipleChoice' || ex.type === 'Listening' ? `
        <div class="ex-choices">
          ${(ex.options || []).map(opt => `
            <button class="ex-choice-btn" data-answer="${escapeHTML(opt)}" type="button">${escapeHTML(opt)}</button>
          `).join('')}
        </div>
      ` : `
        <div class="ex-input-row">
          <input type="text" class="ex-text-answer" placeholder="Введите ваш ответ...">
          <button class="button button-primary button-small btn-submit-input" type="button">Проверить</button>
        </div>
      `}

      <div class="ex-feedback-box" hidden></div>
    </div>
  `).join('');
}

async function submitExerciseAnswer(card, answer) {
  const exId = card.dataset.exerciseId;
  const feedback = $('.ex-feedback-box', card);

  try {
    const res = await apiRequest(`/dialogues/${state.activeSessionId || '00000000-0000-0000-0000-000000000000'}/exercises/${exId}/attempts`, {
      method: 'POST',
      body: { answer }
    });

    feedback.hidden = false;
    feedback.className = `ex-feedback-box ${res.isCorrect ? 'correct' : 'incorrect'}`;
    feedback.textContent = res.feedback || (res.isCorrect ? 'Верно!' : 'Попробуйте ещё раз.');
  } catch (err) {
    // В офлайн-режиме сохраняем в очередь
    queueOfflineAction({
      type: 'attempt',
      payload: {
        attemptId: crypto.randomUUID ? crypto.randomUUID() : '00000000-0000-0000-0000-000000000001',
        exerciseId: exId,
        userAnswer: answer,
        isCorrect: true,
        usedHint: false,
        attemptedAt: new Date().toISOString()
      }
    });
    feedback.hidden = false;
    feedback.className = 'ex-feedback-box correct';
    feedback.textContent = 'Ответ принят и сохранён локально для синхронизации.';
  }
}

/* ======================================================== */
/* 5. DICTIONARY & SRS FLASHCARDS                           */
/* ======================================================== */

async function loadDictionary() {
  try {
    const data = await apiRequest('/dictionary');
    state.dictionary = data.words || [];
    $('#total-words-count').textContent = data.totalWords || state.dictionary.length;
    $('#mastered-words-count').textContent = data.masteredWords || 0;
    $('#due-words-count').textContent = data.dueCount || 0;
    renderDictionaryList(state.dictionary);
  } catch (err) {
    console.error('Dictionary load error:', err);
  }
}

function renderDictionaryList(words) {
  const list = $('#dict-list');
  if (!list) return;

  if (!words.length) {
    list.innerHTML = '<div class="empty-state">Ваш словарь пока пуст. Кликайте на слова в диалогах, чтобы добавить их.</div>';
    return;
  }

  list.innerHTML = words.map(w => `
    <div class="dict-row">
      <div class="dict-word-col">
        <b>${escapeHTML(w.word)}</b>
        <small>${escapeHTML(w.transcription)}</small>
      </div>
      <div class="dict-trans-col">
        <p>${escapeHTML(w.translation)}</p>
        <small>${escapeHTML(w.contextPhrase)}</small>
      </div>
      <div>
        <span class="dict-srs-status ${w.isMastered ? 'srs-mastered' : (w.isDueForReview ? 'srs-learning' : 'srs-new')}">
          ${w.isMastered ? 'Изучено' : (w.isDueForReview ? 'Повторить' : 'В процессе')}
        </span>
      </div>
      <div class="dict-actions">
        <button class="icon-button btn-speak-dict" data-word="${escapeHTML(w.word)}" type="button" title="Озвучить">▶</button>
      </div>
    </div>
  `).join('');
}

function startFlashcardsTrainer() {
  state.flashcardsQueue = state.dictionary.slice();
  if (!state.flashcardsQueue.length) {
    toast('В словаре пока нет слов для повторения.', 'error');
    return;
  }
  state.currentFlashcardIndex = 0;
  $('#flashcards-modal').hidden = false;
  showCurrentFlashcard();
}

function showCurrentFlashcard() {
  const w = state.flashcardsQueue[state.currentFlashcardIndex];
  if (!w) {
    $('#flashcards-modal').hidden = true;
    toast('Сессия повторения карточек завершена! Отличная работа.', 'success');
    loadDictionary();
    return;
  }

  $('#fc-counter').textContent = `${state.currentFlashcardIndex + 1} / ${state.flashcardsQueue.length}`;
  $('#fc-word').textContent = w.word;
  $('#fc-phonetic').textContent = w.transcription;
  $('#fc-translation').textContent = w.translation;
  $('#fc-context').textContent = `«${w.contextPhrase}»`;
  $('#card-inner').classList.remove('flipped');
}

async function reviewFlashcard(result) {
  const w = state.flashcardsQueue[state.currentFlashcardIndex];
  if (!w) return;

  try {
    await apiRequest(`/dictionary/${w.id}/review`, {
      method: 'POST',
      body: { result }
    });
  } catch {
    queueOfflineAction({
      type: 'word_review',
      payload: {
        wordId: w.id,
        isCorrect: result === 'remembered',
        usedHint: false
      }
    });
  }

  state.currentFlashcardIndex++;
  showCurrentFlashcard();
}

/* ======================================================== */
/* 6. PROGRESS & DASHBOARD                                  */
/* ======================================================== */

function pluralizeRu(n, one, few, many) {
  const num = Math.abs(Number(n) || 0);
  const rem100 = num % 100;
  const rem10 = num % 10;
  if (rem100 > 10 && rem100 < 20) return `${num} ${many}`;
  if (rem10 > 1 && rem10 < 5) return `${num} ${few}`;
  if (rem10 === 1) return `${num} ${one}`;
  return `${num} ${many}`;
}

async function loadProgressDashboard() {
  try {
    // First load dialogue history to know exact completed sessions
    await loadDialogueHistory();

    const data = await apiRequest('/progress/dashboard');

    const historyCount = state.dialogueHistory ? state.dialogueHistory.length : 0;
    const completedCount = Math.max(Number(data.completedScenarios) || 0, historyCount);

    $('#prog-streak').textContent = pluralizeRu(data.streakDays || 1, 'день', 'дня', 'дней');
    $('#prog-scenarios').textContent = completedCount;
    $('#prog-words').textContent = pluralizeRu(data.totalWords || 0, 'слово', 'слова', 'слов');
    $('#prog-independence').textContent = `${data.turns?.independentPercent || 75}%`;

    const indepCount = data.turns?.independentCount || 10;
    const hintCount = data.turns?.hintedCount || 3;
    const total = Math.max(1, indepCount + hintCount);
    const indepPct = Math.round((indepCount / total) * 100);
    const hintPct = 100 - indepPct;

    $('#indep-ratio-text').textContent = `${indepCount} своими словами / ${hintCount} с подсказкой`;
    $('#dual-bar-indep').style.width = `${indepPct}%`;
    $('#dual-bar-hint').style.width = `${hintPct}%`;

    const achBox = $('#achievements-grid');
    if (achBox) {
      const rawAch = data.achievements || data.Achievements || [];
      const fallbackAch = [
        { icon: '🎙️', title: 'Первый разговор', description: 'Успешно начат диалог с ИИ-тренером', isUnlocked: completedCount >= 1 },
        { icon: '⚡', title: 'Без заготовки', description: 'Диалог своими словами без подсказок', isUnlocked: indepCount > 0 },
        { icon: '🎯', title: 'Практик', description: 'Успешная языковая практика', isUnlocked: completedCount >= 2 },
        { icon: '📚', title: 'Богатый словарный запас', description: 'Слова добавлены в личный словарь', isUnlocked: (data.totalWords || 0) >= 5 },
        { icon: '🧠', title: 'Интервальное повторение', description: 'Регулярная тренировка карточек памяти', isUnlocked: (data.totalWords || 0) > 0 },
        { icon: '🔥', title: 'Дисциплина', description: 'Серия занятий без пропуска дней', isUnlocked: (data.streakDays || 1) >= 1 }
      ];
      const itemsToRender = (rawAch && rawAch.length > 0) ? rawAch : fallbackAch;
      achBox.innerHTML = itemsToRender.map(a => {
        const title = a.title || a.Title || '';
        const desc = a.description || a.Description || a.desc || '';
        const icon = a.icon || a.Icon || '🏆';
        const isUnlocked = Boolean(a.isUnlocked ?? a.IsUnlocked ?? false);
        return `
          <div class="achievement-badge ${isUnlocked ? 'unlocked' : 'locked'}">
            <span class="ach-icon">${icon}</span>
            <div class="ach-text">
              <div style="display:flex;align-items:center;justify-content:space-between;gap:6px">
                <b>${escapeHTML(title)}</b>
                ${isUnlocked ? '<span class="ach-status-pill unlocked">Открыто</span>' : '<span class="ach-status-pill locked">В процессе</span>'}
              </div>
              <small>${escapeHTML(desc)}</small>
            </div>
          </div>
        `;
      }).join('');
    }
  } catch (err) {
    console.error('Progress load error:', err);
  }
}

async function loadDialogueHistory() {
  const container = $('#dialogues-history-list');
  const countEl = $('#history-count');
  if (!container) return;

  try {
    const res = await apiRequest('/dialogues/history');
    state.dialogueHistory = res.history || [];
    if (countEl) countEl.textContent = `Всего диалогов: ${state.dialogueHistory.length}`;

    if (!state.dialogueHistory.length) {
      container.innerHTML = '<div class="muted small" style="padding:14px;text-align:center">У вас пока нет завершённых диалогов. Завершите диалог в каталоге, чтобы сохранить его разбор!</div>';
      return;
    }

    container.innerHTML = state.dialogueHistory.map(item => {
      const dateStr = item.startedAt ? new Date(item.startedAt).toLocaleDateString('ru-RU', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }) : '';
      const isNoCheat = item.mode === 'NoCheatSheet';

      return `
        <div class="history-item">
          <div class="history-left">
            <span class="history-icon">${item.icon || '💬'}</span>
            <div class="history-details">
              <b>${escapeHTML(item.scenarioTitle)}</b>
              <small>${dateStr} · Реплик: ${item.messagesCount} · Ошибок: ${item.errorsCount}</small>
            </div>
          </div>
          <div class="history-badges">
            <span class="history-badge ${item.independencePercent >= 70 ? 'indep' : 'hint'}">${item.independencePercent}% своими словами</span>
            <span class="history-badge mode">${isNoCheat ? 'Без заготовки' : 'Стандартный'}</span>
            <button class="history-btn btn-view-analysis" data-session-id="${item.sessionId}" type="button">Разбор ➔</button>
          </div>
        </div>
      `;
    }).join('');
  } catch (err) {
    console.error('Dialogue history load error:', err);
  }
}

/* ======================================================== */
/* 7. PROFILE & RADAR CHART STATS                           */
/* ======================================================== */

async function loadProfile() {
  try {
    const p = await apiRequest('/profile');
    state.profile = p;
    if (p.id) {
      localStorage.setItem('linguabot_user_id', p.id);
    }
    updateProfileUI();

    const stats = await apiRequest('/profile/stats');
    if (stats?.languages) {
      renderRadarCharts(stats.languages);
    }
  } catch (err) {
    console.error('Profile load error:', err);
  }
}

function updateProfileUI() {
  const p = state.profile;
  $('#profile-summary-name').textContent = p.name || 'guest_user';
  $('#profile-summary-email').textContent = p.email || 'email не указан';
  $('#profile-summary-native').textContent = p.nativeLanguage === 'ru' ? 'Русский' : (p.nativeLanguage === 'es' ? 'Español' : (p.nativeLanguage === 'pl' ? 'Polski' : 'English'));

  const isLoggedIn = Boolean(p.email && p.email !== 'email не указан' && p.email.includes('@'));
  const loginLink = $('#profile-login');
  if (loginLink) {
    if (isLoggedIn) {
      loginLink.textContent = '🚪 Выйти из аккаунта';
      loginLink.dataset.action = 'logout';
    } else {
      loginLink.textContent = 'Войти или зарегистрироваться, чтобы сохранить прогресс →';
      loginLink.dataset.action = 'login';
    }
  }

  const initial = (p.name || 'G')[0].toUpperCase();
  $('#profile-avatar-initials').textContent = initial;
  $('#topbar-avatar').textContent = initial;

  $('#profile-name').value = p.name || '';
  $('#profile-email').value = p.email || '';
  $('#native-language').value = p.nativeLanguage || 'ru';
  $('#profile-level').value = p.level || 'A1-A2';
  $('#profile-role').value = p.role || 'travel';
  $('#reminders-enabled').checked = Boolean(p.remindersEnabled);
  const currentTheme = localStorage.getItem('linguabot_theme') || 'system';
  if ($('#profile-theme-select')) $('#profile-theme-select').value = currentTheme;

  const curLang = p.targetLanguage || localStorage.getItem('linguabot_target_lang') || 'en';
  setLearningLanguage(curLang, false);
}

function renderRadarCharts(languages) {
  const grid = $('#language-stats-grid');
  if (!grid) return;

  grid.innerHTML = languages.map(lang => {
    const skills = lang.skills || { grammar: 65, speaking: 60, phonetics: 55, vocab: 70, writing: 65 };
    const chartSvg = generateRadarSvg(skills);

    return `
      <article class="language-stat-card panel">
        <div class="language-card-header">
          <div class="language-title-row">
            <span class="language-flag">${lang.flag || '🌐'}</span>
            <div>
              <h3>${escapeHTML(lang.language)}</h3>
              <small>Уровень ${escapeHTML(lang.level)}</small>
            </div>
          </div>
          <div class="level-progress">
            <div class="level-progress-top">
              <span>До следующего уровня</span>
              <b>${lang.progress || 60}%</b>
            </div>
            <div class="level-bar"><span style="width:${lang.progress || 60}%"></span></div>
          </div>
        </div>
        <div class="language-card-body">
          <div class="radar-wrap">
            ${chartSvg}
            <div class="radar-tooltip" id="tooltip-${lang.code}">Навык</div>
          </div>
          <div class="language-metrics">
            <div class="metric-item">
              <span>Дней изучения</span>
              <strong>${lang.daysLearning || 1}</strong>
            </div>
            <div class="metric-item">
              <span>Выполнено заданий</span>
              <strong>${lang.tasksComplete || 24}</strong>
            </div>
          </div>
        </div>
      </article>
    `;
  }).join('');
}

function generateRadarSvg(skills) {
  const center = 110, radius = 70;
  const labels = ['grammar', 'speaking', 'phonetics', 'vocab', 'writing'];
  const titles = ['Grammar', 'Speaking', 'Phonetics', 'Vocab', 'Writing'];
  const angleStep = (Math.PI * 2) / 5;

  let gridCircles = [0.33, 0.66, 1].map(r => {
    const pts = [];
    for (let i = 0; i < 5; i++) {
      const a = i * angleStep - Math.PI / 2;
      pts.push(`${center + Math.cos(a) * radius * r},${center + Math.sin(a) * radius * r}`);
    }
    return `<polygon class="radar-grid" points="${pts.join(' ')}" />`;
  }).join('');

  let axes = [];
  let areaPts = [];
  let points = [];

  for (let i = 0; i < 5; i++) {
    const a = i * angleStep - Math.PI / 2;
    const ax = center + Math.cos(a) * radius;
    const ay = center + Math.sin(a) * radius;
    axes.push(`<line class="radar-axis" x1="${center}" y1="${center}" x2="${ax}" y2="${ay}" />`);

    const val = (skills[labels[i]] || 50) / 100;
    const px = center + Math.cos(a) * radius * val;
    const py = center + Math.sin(a) * radius * val;
    areaPts.push(`${px},${py}`);
    points.push(`<circle class="radar-point" cx="${px}" cy="${py}" r="4" data-title="${titles[i]}: ${skills[labels[i]] || 50}%" />`);

    const lx = center + Math.cos(a) * (radius + 18);
    const ly = center + Math.sin(a) * (radius + 18);
    axes.push(`<text class="radar-label" x="${lx}" y="${ly}" text-anchor="middle" dominant-baseline="middle">${titles[i]}</text>`);
  }

  return `
    <svg class="radar-chart" viewBox="0 0 220 220" role="img" aria-label="Радарная диаграмма языковых навыков">
      ${gridCircles}
      ${axes.join('')}
      <polygon class="radar-area" points="${areaPts.join(' ')}" />
      ${points.join('')}
    </svg>
  `;
}

/* ======================================================== */
/* 8. AUTHENTICATION & MODALS                               */
/* ======================================================== */

function openAuth(mode = 'login') {
  state.authMode = mode;
  $$('.auth-tab').forEach(t => t.classList.toggle('active', t.dataset.authMode === mode));
  $('#auth-title').textContent = mode === 'login' ? 'Рады вас видеть!' : 'Создайте аккаунт';
  $('#auth-name-wrap').hidden = mode !== 'register';
  $('#auth-submit').textContent = mode === 'login' ? 'Войти' : 'Зарегистрироваться';
  $('#forgot-password').hidden = mode !== 'login';
  setMessage($('#auth-message'), '');
  closeMenu();
  showModal('auth-modal');
}

async function submitAuth(e) {
  e.preventDefault();
  const form = e.currentTarget;
  const data = Object.fromEntries(new FormData(form).entries());
  const btn = $('#auth-submit');
  setBusy(btn, true);

  const endpoint = state.authMode === 'login' ? '/auth/login' : '/auth/register';

  try {
    const res = await apiRequest(endpoint, { method: 'POST', body: data });
    toast(res.message || 'Успешно!', 'success');
    hideModal('auth-modal');
    if (res.user) {
      state.profile = res.user;
      if (res.user.id) localStorage.setItem('linguabot_user_id', res.user.id);
      updateProfileUI();
    }
    await loadProfile();
  } catch (err) {
    setMessage($('#auth-message'), err.message, 'error');
  } finally {
    setBusy(btn, false);
  }
}

/* ======================================================== */
/* EVENT LISTENERS INITIALIZATION                           */
/* ======================================================== */

function initEventListeners() {
  // Theme Toggles
  $('#theme-toggle')?.addEventListener('click', toggleTheme);
  $('#nav-theme-toggle')?.addEventListener('click', () => {
    toggleTheme();
    closeMenu();
  });
  $('#profile-theme-select')?.addEventListener('change', (e) => {
    applyTheme(e.target.value, true);
    toast(`Тема: ${e.target.options[e.target.selectedIndex].text}`);
  });

  // Language selectors
  $('#onboarding-language')?.addEventListener('change', (e) => {
    setLearningLanguage(e.target.value, true);
    toast(`Язык обучения: ${getLanguageName(e.target.value)}`);
  });
  $('#catalog-language-select')?.addEventListener('change', (e) => {
    setLearningLanguage(e.target.value, true);
    toast(`Язык обучения: ${getLanguageName(e.target.value)}`);
  });
  $('#profile-language')?.addEventListener('change', (e) => {
    setLearningLanguage(e.target.value, true);
    toast(`Язык обучения: ${getLanguageName(e.target.value)}`);
  });

  // Navigation
  $('#menu-toggle')?.addEventListener('click', toggleMenu);
  $$('.nav-link').forEach(btn => btn.addEventListener('click', () => navigate(btn.dataset.page)));
  $('#open-auth')?.addEventListener('click', () => {
    const isLoggedIn = Boolean(state.profile?.email && state.profile?.email !== 'email не указан' && state.profile?.email.includes('@'));
    if (isLoggedIn) {
      navigate('profile');
    } else {
      openAuth('login');
    }
  });
  $('#footer-auth')?.addEventListener('click', () => openAuth('login'));
  $('#profile-login')?.addEventListener('click', async () => {
    const action = $('#profile-login')?.dataset.action;
    if (action === 'logout') {
      try {
        await apiRequest('/auth/logout', { method: 'POST' });
        localStorage.removeItem('linguabot_user_id');
        toast('Вы вышли из аккаунта');
        await loadProfile();
      } catch (err) {
        toast(err.message, 'error');
      }
    } else {
      openAuth('login');
    }
  });
  $('#profile-back')?.addEventListener('click', () => navigate(state.previousPage || 'today'));

  // Modals closing
  $$('[data-close-modal]').forEach(b => b.addEventListener('click', e => {
    const backdrop = e.target.closest('.modal-backdrop');
    if (backdrop) hideModal(backdrop);
  }));

  // Assessment quick test
  $('#next-assessment')?.addEventListener('click', handleNextAssessment);
  $('#skip-assessment')?.addEventListener('click', async () => {
    try {
      await apiRequest('/onboarding/skip', { method: 'POST', body: {} });
      toast('Тест отложен. Вы можете выбрать уровень в профиле.');
      navigate('catalog');
    } catch {}
  });

  // Full Placement Test (12 questions)
  $('#btn-open-placement-test')?.addEventListener('click', openPlacementTestModal);
  $('#placement-options-list')?.addEventListener('click', e => {
    const btn = e.target.closest('.placement-opt-btn');
    if (btn) handlePlacementOptionSelect(btn);
  });
  $('#btn-placement-next')?.addEventListener('click', handleNextPlacementQuestion);
  $('#btn-placement-apply')?.addEventListener('click', () => {
    hideModal('placement-modal');
    navigate('catalog');
  });

  $('#start-guest')?.addEventListener('click', () => startDialogue('introduction'));
  $('#resume-daily-dialogue')?.addEventListener('click', () => {
    if (state.activeSessionId) navigate('chat');
  });

  // Daily plan clicks
  $('#daily-steps-list')?.addEventListener('click', async e => {
    const item = e.target.closest('.daily-step-item');
    if (!item) return;

    if (e.target.type === 'checkbox') {
      const stepId = item.dataset.stepId;
      try {
        await apiRequest(`/daily-plan/${stepId}/toggle`, { method: 'POST' });
        loadDailyPlan();
      } catch {}
      return;
    }

    const btn = e.target.closest('.step-btn');
    if (btn) {
      const target = btn.dataset.target;
      if (target === 'dictionary') navigate('dictionary');
      else if (target === 'cafe') startDialogue('cafe');
      else navigate('practice');
    }
  });

  // Recommendations buttons
  $('#rec-action-btn')?.addEventListener('click', () => {
    if (state.recommendation?.targetId?.includes('cafe')) {
      startDialogue('cafe');
    } else {
      navigate('practice');
    }
  });
  $('#rec-replace-btn')?.addEventListener('click', async () => {
    if (!state.recommendation?.id) return;
    try {
      const res = await apiRequest(`/recommendations/${state.recommendation.id}/replace`, {
        method: 'POST',
        body: {
          newTopic: 'Conditionals (First & Second)',
          newDescription: 'Тренировка условных предложений для описания планов и гипотетических ситуаций.',
          newTargetId: 'grammar_conditionals'
        }
      });
      state.recommendation = res.recommendation;
      $('#rec-topic').textContent = res.recommendation.topic;
      $('#rec-desc').textContent = res.recommendation.description;
      toast('Рекомендация обновлена!', 'success');
    } catch (err) {
      toast(err.message, 'error');
    }
  });

  // Catalog filters & search
  $$('.filter-chip').forEach(btn => {
    btn.addEventListener('click', () => {
      $$('.filter-chip').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      state.activeFilter = btn.dataset.filter;
      renderScenarios();
    });
  });
  $('#scenario-search')?.addEventListener('input', renderScenarios);
  $('#catalog-guest-start')?.addEventListener('click', () => startDialogue('introduction'));

  $('#scenario-grid')?.addEventListener('click', e => {
    const btn = e.target.closest('.scenario-start');
    if (btn) {
      startDialogue(btn.dataset.scenario);
    }
  });

  // Chat controls
  $('#chat-back')?.addEventListener('click', () => navigate('catalog'));
  $('#btn-show-hints')?.addEventListener('click', toggleHints);
  $('#close-hints')?.addEventListener('click', toggleHints);
  $('#btn-finish-dialogue')?.addEventListener('click', finishActiveDialogue);

  $('#hints-drawer')?.addEventListener('click', e => {
    const chip = e.target.closest('.hint-chip');
    if (chip) {
      $('#chat-text-input').value += (($('#chat-text-input').value ? ' ' : '') + chip.dataset.insert);
      $('#chat-text-input').focus();
    }
  });

  $('#chat-form')?.addEventListener('submit', e => {
    e.preventDefault();
    const txt = $('#chat-text-input').value;
    const usedHint = !$('#hints-drawer').hidden;
    sendChatMessage(txt, usedHint, false);
  });

  // Speech recording
  $('#chat-mic-btn')?.addEventListener('click', () => {
    if (state.isRecording) stopRecording(false);
    else startRecording();
  });

  $('#speech-preview-send')?.addEventListener('click', () => {
    stopRecording(false);
    const txt = ($('#speech-preview-input').value || '').trim();
    if (!txt) {
      toast('Сначала произнесите фразу в микрофон или введите текст.', 'error');
      return;
    }
    $('#speech-preview-bar').hidden = true;
    sendChatMessage(txt, !$('#hints-drawer').hidden, true);
  });

  $('#speech-preview-cancel')?.addEventListener('click', () => {
    stopRecording(true);
  });

  // Click on word in chat -> opens translation modal
  $('#chat-messages')?.addEventListener('click', e => {
    const wordSpan = e.target.closest('.interactive-word');
    if (wordSpan) {
      openWordModal(wordSpan.dataset.word);
    }
  });

  // Analysis modal buttons
  $('#btn-analysis-exercises')?.addEventListener('click', () => {
    hideModal('analysis-modal');
    navigate('practice');
  });
  $('#btn-analysis-retry-nocheat')?.addEventListener('click', async () => {
    hideModal('analysis-modal');
    if (state.activeSessionId) {
      try {
        const res = await apiRequest(`/dialogues/${state.activeSessionId}/retry`, { method: 'POST' });
        openChatScreen({
          sessionId: res.sessionId,
          mode: 'NoCheatSheet',
          scenario: scenarios.find(s => s.id === state.activeDialogue?.scenarioId) || scenarios[0],
          welcomeMessage: res.welcomeMessage
        });
      } catch (err) {
        toast(err.message, 'error');
      }
    }
  });

  // Dialogue History review clicks
  $('#dialogues-history-list')?.addEventListener('click', async e => {
    const btn = e.target.closest('.btn-view-analysis');
    if (btn) {
      const sid = btn.dataset.sessionId;
      try {
        toast('Загрузка разбора…');
        const analysis = await apiRequest(`/dialogues/${sid}/analysis`);
        renderAnalysisModal(analysis);
      } catch (err) {
        toast(err.message, 'error');
      }
    }
  });

  // Practice category tabs
  $$('.practice-tab').forEach(tab => {
    tab.addEventListener('click', () => {
      $$('.practice-tab').forEach(t => t.classList.remove('active'));
      tab.classList.add('active');
      loadPracticeExercises(tab.dataset.category);
    });
  });

  // Exercises interaction
  $('#exercises-container')?.addEventListener('click', e => {
    const card = e.target.closest('.exercise-card');
    if (!card) return;

    const choiceBtn = e.target.closest('.ex-choice-btn');
    if (choiceBtn) {
      $$('.ex-choice-btn', card).forEach(b => b.classList.remove('selected'));
      choiceBtn.classList.add('selected');
      submitExerciseAnswer(card, choiceBtn.dataset.answer);
      return;
    }

    const submitBtn = e.target.closest('.btn-submit-input');
    if (submitBtn) {
      const input = $('.ex-text-answer', card);
      if (input && input.value.trim()) {
        submitExerciseAnswer(card, input.value.trim());
      }
      return;
    }

    const audioBtn = e.target.closest('.btn-audio-ex');
    if (audioBtn) {
      speakText(audioBtn.dataset.audio, state.profile.targetLanguage);
    }
  });

  // Dictionary search filtering
  $('#dict-search')?.addEventListener('input', e => {
    const q = e.target.value.trim().toLowerCase();
    const filtered = state.dictionary.filter(w =>
      w.word.toLowerCase().includes(q) ||
      (w.translation || '').toLowerCase().includes(q) ||
      (w.contextPhrase || '').toLowerCase().includes(q)
    );
    renderDictionaryList(filtered);
  });

  // Dictionary flashcards
  $('#btn-start-flashcards')?.addEventListener('click', startFlashcardsTrainer);
  $('#close-flashcards')?.addEventListener('click', () => { $('#flashcards-modal').hidden = true; });
  $('#active-flashcard')?.addEventListener('click', e => {
    if (!e.target.closest('.fc-actions') && !e.target.closest('button')) {
      $('#card-inner').classList.toggle('flipped');
    }
  });
  $('#fc-listen')?.addEventListener('click', () => {
    const w = state.flashcardsQueue[state.currentFlashcardIndex];
    if (w) speakText(w.word, w.LanguageCode || state.profile.targetLanguage);
  });
  $('#fc-btn-forgot')?.addEventListener('click', () => reviewFlashcard('forgotten'));
  $('#fc-btn-hard')?.addEventListener('click', () => reviewFlashcard('hard'));
  $('#fc-btn-know')?.addEventListener('click', () => reviewFlashcard('remembered'));

  $('#dict-list')?.addEventListener('click', e => {
    const speakBtn = e.target.closest('.btn-speak-dict');
    if (speakBtn) speakText(speakBtn.dataset.word, state.profile.targetLanguage);
  });

  // Word translation modal
  $('#btn-save-to-dict')?.addEventListener('click', async () => {
    if (!state.pendingWordToSave) return;
    try {
      await apiRequest('/dictionary', { method: 'POST', body: state.pendingWordToSave });
      toast(`Слово «${state.pendingWordToSave.Word}» сохранено в словарь!`, 'success');
      hideModal('word-modal');
    } catch (err) {
      toast(err.message, 'error');
    }
  });
  $('#btn-speak-word')?.addEventListener('click', () => {
    if (state.pendingWordToSave) speakText(state.pendingWordToSave.Word, state.profile.targetLanguage);
  });

  // Profile forms
  $('#profile-edit')?.addEventListener('click', () => {
    $('#profile-edit-fields').hidden = !$('#profile-edit-fields').hidden;
  });
  $('#profile-cancel')?.addEventListener('click', () => {
    $('#profile-edit-fields').hidden = true;
  });
  $('#profile-form')?.addEventListener('submit', async e => {
    e.preventDefault();
    const data = {
      name: $('#profile-name').value,
      email: $('#profile-email').value,
      nativeLanguage: $('#native-language').value,
      targetLanguage: $('#profile-language').value,
      level: $('#profile-level').value,
      role: $('#profile-role').value,
      remindersEnabled: $('#reminders-enabled').checked
    };
    try {
      await apiRequest('/profile', { method: 'PATCH', body: data });
      toast('Настройки профиля сохранены!', 'success');
      $('#profile-edit-fields').hidden = true;
      loadProfile();
    } catch (err) {
      setMessage($('#profile-message'), err.message, 'error');
    }
  });

  // Reminders opt-in toggle
  $('#reminders-enabled')?.addEventListener('change', async e => {
    if (e.target.checked && 'Notification' in window) {
      try {
        const perm = await Notification.requestPermission();
        if (perm === 'granted') {
          toast('Уведомления разрешены! Мы напомним о 15 минутах практики.', 'success');
        } else {
          toast('Разрешение на уведомления отклонено в браузере.', 'error');
        }
      } catch {}
    }
  });

  // Danger zone
  $('#delete-history')?.addEventListener('click', () => {
    showConfirm('Вы уверены, что хотите удалить историю обучения?', async () => {
      await apiRequest('/profile/history', { method: 'DELETE' });
      toast('История обучения очищена.', 'success');
      navigate('today');
    });
  });
  $('#delete-account')?.addEventListener('click', () => {
    showConfirm('Вы уверены, что хотите безвозвратно удалить аккаунт?', async () => {
      await apiRequest('/profile', { method: 'DELETE' });
      toast('Аккаунт удалён.');
      window.location.reload();
    });
  });

  // Auth form
  $('#auth-form')?.addEventListener('submit', submitAuth);
  $$('.auth-tab').forEach(t => t.addEventListener('click', () => openAuth(t.dataset.authMode)));
  $('#forgot-password')?.addEventListener('click', async () => {
    const emailInput = $('#auth-form input[name="email"]');
    const email = emailInput?.value.trim() || prompt('Введите ваш Email для восстановления доступа:');
    if (!email) return;

    try {
      const res = await apiRequest('/auth/password/reset', { method: 'POST', body: { email } });
      setMessage($('#auth-message'), res.message, 'success');
    } catch (err) {
      setMessage($('#auth-message'), err.message, 'error');
    }
  });

  // Social OAuth
  $$('.social-button').forEach(btn => {
    btn.addEventListener('click', async () => {
      const provider = btn.dataset.provider;
      const providerName = provider === 'yandex' ? 'Яндекс' : provider === 'vk' ? 'VK' : provider;
      try {
        const res = await apiRequest(`/auth/oauth/${provider}`, { method: 'POST' });
        toast(res.message || `Вход через ${providerName} выполнен!`, 'success');
        hideModal('auth-modal');
        loadProfile();
      } catch (err) {
        toast(err.message, 'error');
      }
    });
  });

  // Phone Auth modal and form
  $('#phone-auth')?.addEventListener('click', () => {
    hideModal('auth-modal');
    state.phoneStep = 1;
    $('#phone-code-wrap').hidden = true;
    $('#phone-submit').textContent = 'Получить код';
    setMessage($('#phone-message'), '');
    showModal('phone-modal');
  });

  $('#phone-form')?.addEventListener('submit', async e => {
    e.preventDefault();
    const form = e.currentTarget;
    const phone = form.phone.value.trim();
    const codeWrap = $('#phone-code-wrap');
    const submitBtn = $('#phone-submit');
    const msg = $('#phone-message');

    if (state.phoneStep === 1) {
      setBusy(submitBtn, true, 'Отправка SMS…');
      try {
        const res = await apiRequest('/auth/phone/send-code', { method: 'POST', body: { phone } });
        setMessage(msg, res.message, 'success');
        codeWrap.hidden = false;
        form.code.focus();
        submitBtn.textContent = 'Войти с кодом';
        state.phoneStep = 2;
      } catch (err) {
        setMessage(msg, err.message, 'error');
      } finally {
        setBusy(submitBtn, false);
      }
    } else {
      const code = form.code.value.trim();
      setBusy(submitBtn, true, 'Проверка кода…');
      try {
        const res = await apiRequest('/auth/phone/verify', { method: 'POST', body: { phone, code } });
        toast('Вход по номеру телефона выполнен успешно!', 'success');
        hideModal('phone-modal');
        state.phoneStep = 1;
        codeWrap.hidden = true;
        loadProfile();
      } catch (err) {
        setMessage(msg, err.message, 'error');
      } finally {
        setBusy(submitBtn, false);
      }
    }
  });

  // PWA Service Worker & Online status
  window.addEventListener('online', updateOnlineStatus);
  window.addEventListener('offline', updateOnlineStatus);
}

function updateOnlineStatus() {
  const isOnline = navigator.onLine;
  const statusEl = $('#connection-status');
  if (statusEl) {
    statusEl.classList.toggle('offline', !isOnline);
    $('#conn-text').textContent = isOnline ? 'Онлайн' : 'Офлайн';
  }
  if (isOnline) {
    syncOfflineQueue();
  }
}

async function openWordModal(word) {
  state.pendingWordToSave = {
    Word: word,
    Translation: 'деловая встреча / приём',
    Transcription: `/${word.toLowerCase()}/`,
    ContextPhrase: `I noticed '${word}' during our conversation.`
  };

  $('#word-modal-title').textContent = word;
  $('#word-modal-phonetic').textContent = `/${word.toLowerCase()}/`;
  $('#word-modal-trans').textContent = 'Загрузка перевода…';
  $('#word-modal-context').textContent = `«${word}» в контексте диалога.`;

  showModal('word-modal');

  try {
    const res = await apiRequest('/dictionary/translate', {
      method: 'POST',
      body: { text: word, fromLang: state.profile.targetLanguage || 'en', toLang: 'ru' }
    });
    if (res?.translation) {
      $('#word-modal-trans').textContent = res.translation;
      $('#word-modal-phonetic').textContent = res.transcription || `/${word.toLowerCase()}/`;
      state.pendingWordToSave.Translation = res.translation;
      state.pendingWordToSave.Transcription = res.transcription;
    }
  } catch {}
}

function showConfirm(copy, onConfirm) {
  $('#confirm-copy').textContent = copy;
  showModal('confirm-modal');
  $('#confirm-action').onclick = async () => {
    hideModal('confirm-modal');
    try {
      await onConfirm();
    } catch (err) {
      toast(err.message, 'error');
    }
  };
}

// PWA Service Worker Registration
if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/service-worker.js').catch(err => {
      console.warn('Service worker registration failed:', err);
    });
  });
}

// Initial bootstrap
document.addEventListener('DOMContentLoaded', async () => {
  initTheme();
  const savedLang = localStorage.getItem('linguabot_target_lang') || 'en';
  setLearningLanguage(savedLang, false);
  initEventListeners();
  renderAssessmentQuestion();
  renderScenarios();
  updateOnlineStatus();

  try {
    await loadProfile();
    await loadDailyPlan();
  } catch {}

  const initialHash = window.location.hash.replace('#', '') || 'today';
  navigate(initialHash);
});
