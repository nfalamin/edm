<footer class="py-8 px-4 md:px-6 bg-transparent relative z-10">
    <div class="max-w-7xl mx-auto glass-panel p-6 md:p-8 rounded-2xl flex flex-col md:flex-row items-center justify-between gap-6 border border-white/10 dark:border-white/5 shadow-xl shadow-navy-950/10">
        <div class="flex flex-col md:flex-row items-center space-y-4 md:space-y-0 md:space-x-4 text-center md:text-left">
            <span class="w-10 h-10 rounded-xl bg-gradient-to-tr from-blue-600 to-cyan-600 flex items-center justify-center font-bold text-white text-lg font-display shadow-md border border-white/10">AH</span>
            <span class="text-xs md:text-sm text-slate-500 font-semibold tracking-widest uppercase">© <span x-text="new Date().getFullYear()"></span> Alamin Hossain. <span class="block md:inline mt-1 md:mt-0 text-slate-400">All Rights Reserved.</span></span>
        </div>
        <div class="flex items-center space-x-6 text-slate-400">
            <a href="#" class="hover:text-blue-500 hover:-translate-y-1 transition-all duration-300 text-xl"><i class="fa-brands fa-linkedin-in"></i></a>
            <a href="#" class="hover:text-cyan-400 hover:-translate-y-1 transition-all duration-300 text-xl"><i class="fa-brands fa-twitter"></i></a>
            <a href="#" class="hover:text-blue-600 hover:-translate-y-1 transition-all duration-300 text-xl"><i class="fa-brands fa-facebook"></i></a>
        </div>
    </div>
</footer>

<!-- FLOAT AI CHATBOT WIDGET -->
<div x-data="aiChatbot()" class="fixed bottom-6 right-6 z-[100] flex flex-col items-end font-sans">
    <div x-show="isOpen" class="w-[90vw] sm:w-[360px] h-[500px] max-h-[75vh] mb-4 rounded-3xl shadow-2xl flex flex-col overflow-hidden border border-slate-200 dark:border-white/10 bg-slate-50/95 dark:bg-navy-900/95 backdrop-blur-xl" x-cloak>
        <div class="p-4 border-b border-slate-200 dark:border-white/5 bg-white dark:bg-navy-950 flex items-center justify-between shadow-sm">
            <div class="flex items-center space-x-3">
                <div class="relative">
                    <img src="<?php echo get_template_directory_uri(); ?>/nf011.png" alt="Al Amin Hossain" class="w-10 h-10 rounded-full object-cover border-2 border-cyan-500 bg-navy-800">
                    <span class="absolute bottom-0 right-0 w-3 h-3 bg-emerald-500 border-2 border-white dark:border-navy-950 rounded-full"></span>
                </div>
                <div class="flex flex-col text-left">
                    <span class="text-sm font-bold text-slate-900 dark:text-white font-display leading-tight">Al Amin Hossain</span>
                    <span class="text-[10px] text-slate-500 dark:text-slate-400 font-medium">SEO Expert</span>
                </div>
            </div>
            <button @click="toggle()" class="text-slate-400 hover:text-white transition-colors w-8 h-8 rounded-full"><i class="fa-solid fa-times"></i></button>
        </div>

        <div class="flex-1 overflow-y-auto p-4 space-y-4 scrollbar-hide" x-ref="chatBody">
            <template x-for="(msg, index) in messages" :key="index">
                <div class="flex w-full" :class="msg.sender === 'user' ? 'justify-end' : 'justify-start'">
                    <div class="max-w-[85%] rounded-2xl px-4 py-2.5 text-sm shadow-sm" :class="msg.sender === 'user' ? 'bg-gradient-to-r from-blue-600 to-cyan-600 text-white rounded-br-sm' : 'bg-white dark:bg-navy-800 border border-slate-100 dark:border-white/5 text-slate-700 dark:text-slate-200 rounded-bl-sm'">
                        <span x-html="msg.text"></span>
                    </div>
                </div>
            </template>
        </div>

        <div class="p-3 bg-white dark:bg-navy-950 border-t border-slate-200 dark:border-white/5 relative z-10">
            <form @submit.prevent="sendMessage()" class="flex items-center space-x-2">
                <input type="text" x-model="userInput" placeholder="Ask me anything..." class="flex-1 bg-slate-100 dark:bg-navy-900 border border-slate-200 dark:border-white/10 rounded-xl px-4 py-3 text-sm focus:outline-none focus:border-cyan-500">
                <button type="submit" class="w-11 h-11 rounded-xl bg-gradient-to-tr from-blue-600 to-cyan-600 text-white flex items-center justify-center hover:shadow-lg"><i class="fa-solid fa-paper-plane text-sm"></i></button>
            </form>
        </div>
    </div>
    
    <button @click="toggle()" class="w-12 h-12 sm:w-14 sm:h-14 bg-gradient-to-tr from-blue-600 to-cyan-500 text-white rounded-full flex items-center justify-center text-xl sm:text-2xl shadow-xl transition-all duration-300 hover:scale-105 hover:shadow-cyan-500/30 border border-white/20 relative cursor-pointer" aria-label="Open AI Assistant">
        <i class="fa-solid fa-sparkles text-lg" x-show="!isOpen"></i>
        <i class="fa-solid fa-chevron-down text-lg" x-show="isOpen" x-cloak></i>
        <span class="absolute -top-1 -right-1 w-3.5 h-3.5 bg-emerald-400 border-2 border-slate-950 rounded-full animate-pulse" x-show="!isOpen"></span>
    </button>
</div>