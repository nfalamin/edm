const express = require('express');
const http = require('http');
const path = require('path');
const fs = require('fs');
const cors = require('cors');
const TelegramBot = require('node-telegram-bot-api');
const { Server } = require('socket.io');
const { GoogleGenerativeAI } = require('@google/generative-ai');

require('dotenv').config();

const app = express();
app.use(cors());
app.use(express.json({ limit: '1mb' }));
app.use(express.urlencoded({ extended: true }));
app.use((req, res, next) => {
  res.setHeader('X-Content-Type-Options', 'nosniff');
  next();
});

const server = http.createServer(app);
const io = new Server(server, {
  cors: {
    origin: '*',
    methods: ['GET', 'POST']
  }
});

const TELEGRAM_TOKEN = process.env.TELEGRAM_BOT_TOKEN || process.env.TELEGRAM_TOKEN;
const MY_CHAT_ID = process.env.MY_CHAT_ID || process.env.TELEGRAM_CHAT_ID;
const GEMINI_API_KEY = process.env.GEMINI_API_KEY || '';
const PAYMENT_TOKEN = process.env.SMAT_GLOBAL_TOKEN || process.env.PAYMENT_GATEWAY_TOKEN;
const PORT = process.env.PORT || 3000;

const KNOWLEDGE_FILE = path.join(__dirname, 'chatbot-data.json');
let knowledgeData = {};

try {
  knowledgeData = JSON.parse(fs.readFileSync(KNOWLEDGE_FILE, 'utf8'));
} catch (error) {
  console.warn('Could not load chatbot-data.json, using empty knowledge base.', error.message);
}

const bot = TELEGRAM_TOKEN
  ? new TelegramBot(TELEGRAM_TOKEN, {
      polling: {
        interval: 300,
        autoStart: true,
        params: { timeout: 10 }
      }
    })
  : null;
const liveChatSessions = new Set();
const conversationHistory = new Map();

let genAI = null;
if (GEMINI_API_KEY && GEMINI_API_KEY.trim() !== '') {
  genAI = new GoogleGenerativeAI(GEMINI_API_KEY);
}

function normalizeText(text = '') {
  return text.toLowerCase().replace(/[^a-z0-9\s]/gi, ' ').replace(/\s+/g, ' ').trim();
}

function getKeywordScore(text, keywords = []) {
  const normalized = normalizeText(text);
  let score = 0;
  keywords.forEach((keyword) => {
    if (normalized.includes(normalizeText(keyword))) score += 1;
  });
  return score;
}

function buildKnowledgeReply(message) {
  const text = message || '';
  const lower = normalizeText(text);

  if (getKeywordScore(text, ['price', 'pricing', 'cost', 'budget', 'package']) > 0) {
    return knowledgeData.pricing?.summary ||
      'Pricing depends on the scope of work. I can share package options and estimate based on your goals.';
  }

  if (getKeywordScore(text, ['service', 'services', 'what do you do', 'offer']) > 0) {
    const services = (knowledgeData.services || []).map((s) => s.name).join(', ');
    return `I mainly help with: ${services}. If you tell me your goal, I can recommend the best fit.`;
  }

  if (getKeywordScore(text, ['portfolio', 'project', 'work', 'case study', 'example']) > 0) {
    const highlights = (knowledgeData.portfolioHighlights || []).map((item) => `${item.project} — ${item.result}`).join(' ');
    return `Here are some highlights of my work: ${highlights}`;
  }

  if (getKeywordScore(text, ['skill', 'skills', 'expertise', 'seo', 'ads', 'marketing']) > 0) {
    const skills = (knowledgeData.skills || []).join(', ');
    return `My core expertise includes: ${skills}.`;
  }

  if (getKeywordScore(text, ['contact', 'email', 'website', 'telegram']) > 0) {
    return `You can reach out through ${knowledgeData.contact?.email || 'the contact form'} or visit ${knowledgeData.contact?.website || 'my website'}.`;
  }

  const faqMatch = (knowledgeData.faqs || []).find((item) => {
    const q = normalizeText(item.question);
    return q.includes(lower) || lower.includes(q) || getKeywordScore(text, [item.question]) > 0;
  });

  if (faqMatch) {
    return faqMatch.answer;
  }

  return null;
}

function shouldEscalate(message) {
  const urgentTerms = [
    'urgent', 'emergency', 'deadline', 'refund', 'legal', 'contract', 'invoice',
    'payment issue', 'dispute', 'custom proposal', 'quote', 'speak to human',
    'talk to someone', 'live chat', 'need manager', 'can i call', 'meeting',
    'question not answered', 'difficult', 'private details'
  ];
  const text = normalizeText(message);
  return urgentTerms.some((term) => text.includes(normalizeText(term)));
}

async function sendTelegramAlert(payload) {
  if (!bot || !MY_CHAT_ID) {
    console.warn('Telegram alert skipped because token/chat id missing.');
    return;
  }

  const text = [
    '🚨 New chatbot escalation request',
    `User: ${payload.userName || 'Website visitor'}`,
    `Session: ${payload.sessionId || 'unknown'}`,
    `Query: ${payload.message}`,
    `Context: ${payload.context || 'No extra context provided'}`,
    `Time: ${new Date().toISOString()}`
  ].join('\n');

  try {
    await bot.sendMessage(MY_CHAT_ID, text, { parse_mode: 'Markdown' });
  } catch (error) {
    console.error('Telegram send failed:', error.message);
  }
}

async function generateAIResponse(message, context = {}) {
  const knowledgeReply = buildKnowledgeReply(message);
  const likelyUrgent = shouldEscalate(message);

  if (likelyUrgent || (!knowledgeReply && context.isFallback)) {
    await sendTelegramAlert({
      message,
      sessionId: context.sessionId || 'guest',
      context: JSON.stringify(context)
    });
    return 'Thanks for reaching out — I have already forwarded your request to Al Amin on Telegram so he can respond directly.';
  }

  if (knowledgeReply) {
    return knowledgeReply;
  }

  if (genAI) {
    try {
      const model = genAI.getGenerativeModel({ model: 'gemini-1.5-flash' });
      const prompt = [
        'You are a professional AI assistant for Al Amin Hossain, a digital marketing and SEO expert.',
        `Website context: ${JSON.stringify(knowledgeData, null, 2)}`,
        'Rules:',
        '- Answer in a friendly, professional, concise way.',
        '- Use the website information as the primary knowledge base.',
        '- If the user asks for pricing, provide a helpful estimate and ask what service they need.',
        '- If the message seems urgent or highly specific, ask the user to wait while you notify the owner.',
        `User query: ${message}`
      ].join('\n');
      const result = await model.generateContent(prompt);
      const text = result.response.text();
      if (text && text.trim()) return text;
    } catch (error) {
      console.error('Gemini response failed:', error.message);
    }
  }

  return 'Thanks for your message! I can help with services, portfolio highlights, pricing, and general questions. If you need a custom proposal or urgent help, please say so and I will notify Al Amin.';
}

function getSessionHistory(sessionId) {
  return conversationHistory.get(sessionId) || [];
}

function saveSessionHistory(sessionId, history) {
  conversationHistory.set(sessionId, history.slice(-12));
}

app.get('/api/chatbot/health', (req, res) => {
  res.json({
    status: 'ok',
    botConfigured: Boolean(TELEGRAM_TOKEN),
    telegramChatIdConfigured: Boolean(MY_CHAT_ID),
    paymentConfigured: Boolean(PAYMENT_TOKEN)
  });
});

app.post('/api/chatbot', async (req, res) => {
  try {
    const { message = '', sessionId = 'guest' } = req.body;
    if (!message.trim()) {
      return res.status(400).json({ success: false, reply: 'Please enter a message.' });
    }

    const history = getSessionHistory(sessionId);
    const context = {
      sessionId,
      historyLength: history.length,
      isFallback: !buildKnowledgeReply(message)
    };

    const reply = await generateAIResponse(message, context);
    const updatedHistory = [...history, { role: 'user', text: message }, { role: 'bot', text: reply }];
    saveSessionHistory(sessionId, updatedHistory);

    res.json({
      success: true,
      reply,
      sessionId,
      needsHuman: shouldEscalate(message)
    });
  } catch (error) {
    console.error('Chatbot API error:', error);
    res.status(500).json({ success: false, reply: 'Something went wrong. Please try again in a moment.' });
  }
});

app.post('/api/payment/initiate', (req, res) => {
  try {
    const { amount, currency = 'USD', customerName = 'Client', service = 'Consultation' } = req.body;

    if (!PAYMENT_TOKEN) {
      return res.status(500).json({
        success: false,
        message: 'Payment gateway token is not configured yet.'
      });
    }

    const paymentPayload = {
      gateway: 'Smat Global',
      tokenConfigured: true,
      amount: Number(amount || 0),
      currency,
      customerName,
      service,
      note: 'This payload is ready to be sent to your payment provider endpoint when you configure the exact API URL.',
      headers: {
        Authorization: `Bearer ${PAYMENT_TOKEN}`,
        'Content-Type': 'application/json'
      }
    };

    res.json({ success: true, payment: paymentPayload });
  } catch (error) {
    console.error('Payment init error:', error);
    res.status(500).json({ success: false, message: 'Unable to create payment request.' });
  }
});

io.on('connection', (socket) => {
  console.log(`Website visitor connected: ${socket.id}`);

  socket.on('user_message', async (msg) => {
    const sessionId = socket.id;
    const context = {
      sessionId,
      isFallback: !buildKnowledgeReply(msg)
    };

    const aiReply = await generateAIResponse(msg, context);
    if (shouldEscalate(msg)) {
      liveChatSessions.add(sessionId);
    }
    socket.emit('bot_reply', aiReply);
  });
});

if (bot) {
  bot.on('polling_error', (error) => {
    console.error('Telegram polling error:', error.response?.description || error.message || error);
  });

  bot.getMe()
    .then((me) => {
      console.log(`Telegram bot connected successfully: @${me.username}`);
    })
    .catch((error) => {
      console.error('Telegram auth failed. Check the bot token:', error.response?.description || error.message || error);
    });

  bot.on('message', (msg) => {
    const chatId = msg.chat.id.toString();

    if (chatId === MY_CHAT_ID && msg.reply_to_message && msg.reply_to_message.text) {
      const match = msg.reply_to_message.text.match(/Session:\s*([^\n]+)/);
      if (match && match[1]) {
        const sessionId = match[1].trim();
        liveChatSessions.add(sessionId);
        io.to(sessionId).emit('bot_reply', msg.text);
      }
    }
  });
}

app.use((err, req, res, next) => {
  console.error('Server error:', err.stack || err);
  res.status(err.status || 500).json({
    success: false,
    message: 'Internal server error. Please try again shortly.'
  });
});

server.on('error', (error) => {
  if (error.code === 'EADDRINUSE') {
    console.error(`Port ${PORT} is already in use. Please stop the other process or change PORT in your .env file.`);
    process.exit(1);
  } else {
    console.error('Server startup error:', error);
    process.exit(1);
  }
});

server.listen(PORT, () => {
  console.log(`AI chatbot server running on port ${PORT}`);
});

process.on('SIGINT', () => {
  console.log('Shutting down chatbot server...');
  if (bot && typeof bot.stopPolling === 'function') {
    bot.stopPolling();
  }
  server.close(() => process.exit(0));
});