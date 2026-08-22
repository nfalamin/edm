require('dotenv').config();
const TelegramBot = require('node-telegram-bot-api');

// .env ফাইল থেকে আপনার টেলিগ্রাম বটের টোকেন ইম্পোর্ট করুন
const TELEGRAM_TOKEN = process.env.TELEGRAM_TOKEN;

// বটটি পোলিং মোডে চালু করুন
const bot = new TelegramBot(TELEGRAM_TOKEN, { polling: true });

// কেউ মেসেজ দিলে বট এই রিপ্লাই দেবে
bot.on('message', (msg) => {
  const chatId = msg.chat.id;
  bot.sendMessage(chatId, 'আমি আপনার বার্তা পেয়েছি!');
});