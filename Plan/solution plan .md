# 🛠️ EDM ফিক্স প্রম্পট প্যাক — Google Antigravity-র জন্য

## উত্তর সংক্ষেপে

**মোট প্রম্পট: ২১টি** — ৭টি ফেজে সাজানো।
কেন ২১টি? কারণ ৭টি Critical + ২১টি High + ২৬টি Medium + ১১টি Low একসাথে একটি প্রম্পটে দিলে যেকোনো এজেন্ট অর্ধেক কাজ করে থেমে যাবে। প্রতিটি প্রম্পট এমনভাবে ভাগ করেছি যাতে **একটি প্রম্পট = একটি স্বতন্ত্র, বিল্ড-করা, কমিট-করা যোগ্য কাজ**।

## ⚠️ ব্যবহারের নিয়ম (এটা আগে পড়ুন)

1. **ক্রম অনুসারে চালান** — P1 থেকে P21। ক্রম গুরুত্বপূর্ণ, কারণ P13 (direct write) P10-এর উপর নির্ভরশীল।
2. **একবারে একটি প্রম্পট।** দুটো একসাথে দেবেন না।
3. প্রতিটি প্রম্পটের পর: `dotnet build` → `dotnet test` → **আলাদা গিট কমিট**। কিছু ভাঙলে শুধু সেই কমিট রিভার্ট করবেন।
4. **P1 আপনি নিজে করবেন, এজেন্ট দিয়ে নয়** — টোকেন রোটেশন ও গিট হিস্টরি রিরাইট এজেন্টকে দেওয়া বিপজ্জনক।
5. P12 ও P13 সবচেয়ে বড় দুটো — এগুলোর জন্য আলাদা ব্রাঞ্চ বানাবেন।

| ফেজ | প্রম্পট | বিষয় |
|---|---|---|
| ০ | P1 | 🔴 সিক্রেট রোটেশন (ম্যানুয়াল) |
| ১ | P2–P6 | 🔴 Auth Critical (API) |
| ২ | P7–P8 | 🔴 ড্যাশবোর্ড XSS ও আর্কিটেকচার |
| ৩ | P9–P11 | 🟠 ডাউনলোড ইঞ্জিন ডেটা ইন্টিগ্রিটি |
| ৪ | P12–P15 | ⚡ ইঞ্জিন পারফরম্যান্স (IDM-কে হারানোর মূল কাজ) |
| ৫ | P16 | 🔴 ভার্সন সমন্বয় |
| ৬ | P17–P18 | 🌐 ওয়েবসাইট ও পোর্টফোলিও |
| ৭ | P19–P21 | 🧹 হাইজিন ও রিগ্রেশন টেস্ট |

---

# ফেজ ০ — ইমার্জেন্সি

## P1 · সিক্রেট রোটেশন (⚠️ এজেন্টকে দেবেন না, নিজে করুন)

এটা প্রম্পট নয়, চেকলিস্ট। এজেন্ট দিয়ে গিট হিস্টরি রিরাইট করালে রিপো নষ্ট হতে পারে।

```text
১. Telegram BotFather-এ গিয়ে bot token রিভোক ও নতুন issue করুন
২. Smat Global ড্যাশবোর্ডে গিয়ে payment token রিভোক ও নতুন issue করুন
৩. website/my portfolio/.env ফাইল ডিলিট করুন, নতুন টোকেন সার্ভারের
   environment variable-এ রাখুন (ফাইলে নয়)
৪. .gitignore-এ যোগ করুন:  .env  এবং  *.env
৫. .env.example তৈরি করুন (শুধু কী-নাম, কোনো মান নয়)
৬. git filter-repo দিয়ে হিস্টরি থেকে .env পার্জ করুন, তারপর
   তিনটি রিমোটে (origin/main, origin/edm-23-8-2026, origin/version-2)
   force-push করুন
৭. release-manifest.json-এ "secretsScanClean": true → false করুন
   (P16-এ ঠিক হবে)
```

---

# ফেজ ১ — Auth Critical

## P2 · টোকেন ডিসক্লোজার ও 2FA গার্ড

```text
EDM.ControlPlane.Api-তে তিনটি অথেন্টিকেশন দুর্বলতা ঠিক করো। কোনো নতুন ফিচার
যোগ করবে না, শুধু এই তিনটি ঠিক করবে।

১. CRITICAL — পাসওয়ার্ড রিসেট টোকেন ফাঁস:
   Services/AuthService.cs-এর ForgotPasswordAsync (≈লাইন 1112) প্লেইনটেক্সট
   রিসেট টোকেনকে AccessToken ফিল্ডে রিটার্ন করছে, এবং
   Controllers/AuthController.cs (≈লাইন 459) সেটি JSON রেসপন্সে resetToken
   নামে পাঠাচ্ছে। এটি সম্পূর্ণ অননুমোদিত — যে কেউ অ্যাডমিনের ইমেইল দিয়ে
   টোকেন নিয়ে পাসওয়ার্ড বদলে ফেলতে পারে।
   → রেসপন্স থেকে resetToken সম্পূর্ণ সরাও। AuthService যেন টোকেন রিটার্ন না
     করে। সব ক্ষেত্রে (ইউজার আছে বা নেই) একই জেনেরিক মেসেজ ও একই HTTP
     স্ট্যাটাস দাও, যাতে user-enumeration oracle না থাকে।

২. HIGH — রিকভারি-ইমেইল ভেরিফিকেশন টোকেন ফাঁস:
   AuthService.cs-এর RequestRecoveryEmailChangeAsync (≈লাইন 1003) এবং
   AuthController.cs (≈লাইন 296) একইভাবে verificationToken রিটার্ন করছে।
   → একই ভাবে সরাও।

৩. HIGH — 2FA সিক্রেট রি-অথ ছাড়াই রোটেট হয়:
   AuthService.cs-এর Generate2FaSetupAsync (≈লাইন 795) নতুন TwoFactorSecret
   লিখে দেয়, কোনো পাসওয়ার্ড পুনঃযাচাই ছাড়াই এবং TwoFactorEnabled গার্ড
   ছাড়াই। ফলে সেশন-হাইজ্যাক হলে আক্রমণকারী ভিকটিমের 2FA নিজের অ্যাপে সরিয়ে
   নিতে পারে।
   → বর্তমান পাসওয়ার্ড বাধ্যতামূলক করো। TwoFactorEnabled == true হলে
     বিদ্যমান TOTP কোডও চাও। কেবল সফল যাচাইয়ের পর সিক্রেট রোটেট করো।

৪. HIGH — AuthController.cs-এর 2fa/confirm এন্ডপয়েন্টে লোকাল catch ব্লক
   ex.Message ও ex.ToString() রেসপন্সে ফেরত দিচ্ছে, গ্লোবাল exception handler
   বাইপাস করে। → লোকাল catch সরাও, গ্লোবাল হ্যান্ডলারকেই কাজ করতে দাও।

শেষে: dotnet build চালাও, এবং প্রতিটি পরিবর্তিত ফাইল+লাইন উল্লেখ করে রিপোর্ট দাও।
```

## P3 · WebAuthn / Passkey — ভুয়া ভেরিফিকেশন

```text
EDM.ControlPlane.Api/Services/PasskeyService.cs-এ একটি CRITICAL
authentication bypass আছে।

সমস্যা:
VerifyAssertion(clientDataJson, authenticatorData, signature, storedPublicKey,
lastSignCount, out newSignCount) মেথডটি signature ও storedPublicKey — দুটো
প্যারামিটারই গ্রহণ করে কিন্তু মেথড বডিতে কখনো ব্যবহার করে না। এটি শুধু
clientDataJson পার্স করে, type == "webauthn.get" মেলায়, চ্যালেঞ্জ দেখে, তারপর
true রিটার্ন করে। এবং newSignCount = lastSignCount + 1 বসিয়ে দেয়।

যে যাচাইগুলো সম্পূর্ণ অনুপস্থিত: ক্রিপ্টোগ্রাফিক signature verification,
origin যাচাই, rpIdHash যাচাই, sign-count regression detection।
VerifyRegistration-ও একইভাবে ভুয়া — যেকোনো attestationObject গ্রহণ করে এবং
ক্লায়েন্ট-প্রদত্ত ডেটাকেই publicKey হিসেবে সংরক্ষণ করে।

Controllers/AuthController.cs-এর passkey/login-options ও passkey/login-verify
দুটোই unauthenticated, তাই যে কেউ একটি পরিচিত credentialId ও ডামি signature
দিয়ে সম্পূর্ণ সেশন পেয়ে যেতে পারে।

দুই ধাপে ঠিক করো:

ধাপ ১ (এখনই, অগ্রাধিকার):
passkey-সম্পর্কিত সব এন্ডপয়েন্ট (register-options, register-verify,
login-options, login-verify) একটি feature flag-এর পিছনে রাখো, যার ডিফল্ট মান
disabled। Disabled থাকলে 404 বা 501 দাও। ড্যাশবোর্ডের UI থেকেও passkey
অপশন লুকাও। হাতে লেখা WebAuthn নিরাপদভাবে করা প্রায় অসম্ভব, তাই এটি
সক্রিয় রেখে শিপ করা যাবে না।

ধাপ ২:
Fido2NetLib NuGet প্যাকেজ যোগ করো এবং PasskeyService-এর ভেতরটা সম্পূর্ণ
সেই লাইব্রেরি দিয়ে প্রতিস্থাপন করো — registration attestation যাচাই,
assertion signature যাচাই, origin ও rpIdHash যাচাই, sign-count regression
ডিটেকশন সবই লাইব্রেরিকে করতে দাও। নিজে কোনো ক্রিপ্টো লিখবে না।
appsettings.json-এ Passkeys সেকশন যোগ করো (RelyingPartyId, RelyingPartyName,
Origins) — বর্তমানে কী-টি অনুপস্থিত তাই PasskeyService.cs লাইন 30-এ
প্রোডাকশনেও RP ID "localhost" হয়ে যায়। কোনো hardcoded ডিফল্ট রাখবে না,
কনফিগ না থাকলে স্টার্টআপে throw করো।
এছাড়া static _challenges ডিকশনারির O(n) sweep-কে একটি IMemoryCache বা
expiry-সহ কালেকশনে সরাও।

শেষে dotnet build + dotnet test চালাও এবং রিপোর্ট দাও।
```

## P4 · Google ID টোকেন ভেরিফিকেশন

```text
EDM.ControlPlane.Api/Services/GoogleAuthService.cs-এ একটি CRITICAL
authentication bypass আছে।

সমস্যা (লাইন 37):
handler.ReadJwtToken(idToken) — এটি টোকেনটি শুধু ডিকোড করে, সিগনেচার যাচাই
করে না। Google-এর JWKS endpoint থেকে পাবলিক কী আনা হয় না, ValidateToken()
কল হয় না, ValidateIssuerSigningKey নেই। এরপর যে তিনটি চেক আছে (লাইন 40
issuer, লাইন 46 expiry, লাইন 52-64 audience) — সবই আক্রমণকারীর নিয়ন্ত্রণে
থাকা, অযাচাইকৃত পেলোড থেকে পড়া। audience কোনো সিক্রেট নয়, এটি ক্লায়েন্ট
কোডে পাবলিক।

শোষণ: আক্রমণকারী নিজেই একটি JWT বানায় — iss = "https://accounts.google.com",
aud = পাবলিক client ID, ভবিষ্যতের exp, email = টার্গেট অ্যাডমিনের ইমেইল।
সিগনেচারের জায়গায় যা-খুশি। তারপর /api/v1/auth/google/login-এ পোস্ট।
AuthService.cs-এর VerifyGoogleLoginAsync (≈লাইন 412) ইউজার খোঁজে
u.Email == email দিয়ে — অর্থাৎ সম্পূর্ণ অ্যাডমিন সেশন পাওয়া যায়।

যা করতে হবে:

১. Google.Apis.Auth NuGet প্যাকেজ যোগ করো এবং ValidateGoogleTokenAsync-এর
   ভেতরের সম্পূর্ণ ম্যানুয়াল লজিক GoogleJsonWebSignature.ValidateAsync
   দিয়ে প্রতিস্থাপন করো (এটি JWKS ফেচ করে প্রকৃত signature verification
   করে)। ValidationSettings-এ Audience হিসেবে expected client ID দাও।
   JwtSecurityTokenHandler ও ReadJwtToken সম্পূর্ণ সরাও।

২. GOOGLE_CLIENT_ID কনফিগার করা না থাকলে Google লগইন সম্পূর্ণ disable করো
   (বর্তমানে audience চেকটাই skip হয়ে যায় — এটি আরও খারাপ)।

৩. email_verified == true না হলে লগইন reject করো।

৪. AuthService.cs-এর VerifyGoogleLoginAsync-এ email-ভিত্তিক অ্যাকাউন্ট
   লিংকিং বন্ধ করো। বর্তমানে GoogleSubjectId খালি থাকলে প্রথম-আসা sub
   স্থায়ীভাবে বেঁধে দেওয়া হয় (first-come account linking) — এটি সিগনেচার
   ঠিক করার পরেও একটি টেকওভার প্যাটার্ন। শুধু GoogleSubjectId ম্যাচে লগইন
   অনুমোদন করো; লিংকিং আলাদা authenticated ফ্লো-তে সরাও।

৫. একই মেথডে detailsJson স্ট্রিং ইন্টারপোলেশন দিয়ে তৈরি হচ্ছে
   (≈লাইন 421), যেখানে googleSubject অ্যাটাকার-নিয়ন্ত্রিত — এটি অডিট লগে
   JSON injection। JsonSerializer ব্যবহার করো।

শেষে dotnet build + dotnet test চালাও এবং রিপোর্ট দাও।
```

## P5 · রেট লিমিটিং সক্রিয় করা

```text
EDM.ControlPlane.Api-তে রেট লিমিটিং সম্পূর্ণ অকার্যকর — এটি CRITICAL, কারণ
এটি অন্য সব auth দুর্বলতাকে স্বয়ংক্রিয়ভাবে শোষণযোগ্য করে তোলে।

সমস্যা:
Program.cs লাইন 169-184-এ "AuthRateLimit" পলিসি (১০ req/মিনিট প্রতি IP)
সঠিকভাবে ডিফাইন করা এবং app.UseRateLimiter() পাইপলাইনেও আছে। কিন্তু পুরো
কোডবেসে EnableRateLimiting অ্যাট্রিবিউট খুঁজে একটিও ম্যাচ নেই — অর্থাৎ
পলিসিটি কোনো এন্ডপয়েন্টে অ্যাটাচ করা হয়নি, এবং কোনো global limiter-ও নেই।
ফলাফল: সম্পূর্ণ API আনথ্রটলড।

যা করতে হবে:

১. একটি GlobalLimiter কনফিগার করো যা সব রিকোয়েস্টে প্রযোজ্য (উদার লিমিট,
   যেমন ১০০-২০০/মিনিট প্রতি IP), যাতে কোনো এন্ডপয়েন্ট ভুলে বাদ পড়লেও
   সুরক্ষিত থাকে।

২. AuthRateLimit পলিসি AuthController-এর সব সংবেদনশীল এন্ডপয়েন্টে অ্যাটাচ
   করো: login, register, google/login, 2fa/verify, 2fa/confirm,
   forgot-password, reset-password, recovery-email/*, এবং setup-initial-admin।
   forgot-password ও reset-password-এ আরও কঠোর লিমিট দাও (যেমন ৩/ঘণ্টা)।

৩. Argon2id প্রতি হ্যাশে ৬৪ MB × ৪ থ্রেড খরচ করে। তাই login ও register-এ
   একটি concurrency limiter-ও যোগ করো, যাতে সমান্তরাল হ্যাশিং সীমিত থাকে
   (মেমরি-এক্সহশন DoS প্রতিরোধ)।

৪. Program.cs-এ UseForwardedHeaders() যোগ করো, ForwardedHeaders-এ
   XForwardedFor ও XForwardedProto সহ, এবং KnownProxies/KnownNetworks
   কনফিগারযোগ্য allow-list দাও। বর্তমানে partition key
   Connection.RemoteIpAddress, কিন্তু কোনো forwarded-headers মিডলওয়্যার
   নেই — রিভার্স প্রক্সির পেছনে সব রিকোয়েস্ট একটি IP-তে জমা হবে, তাই
   লিমিটার ঠিক করলেও ভুলভাবে কাজ করবে।

৫. Program.cs লাইন 433 vs 440 — বর্তমানে static file middleware রেট
   লিমিটারের আগে আছে, তাই ৩৪ MB ইনস্টলার সম্পূর্ণ আনথ্রটলড সার্ভ হয়।
   UseRateLimiter()-কে static files-এর আগে সরাও এবং ডাউনলোড পাথে একটি
   আলাদা bandwidth-সচেতন পলিসি দাও।

৬. রেট লিমিট ছাড়িয়ে গেলে 429 + Retry-After হেডার দাও।

শেষে dotnet build চালাও, একটি integration test লেখো যা প্রমাণ করে ১১তম
login attempt 429 পায়, এবং রিপোর্ট দাও।
```

## P6 · JWT কী ও হার্ডকোডেড ক্রেডেনশিয়াল

```text
EDM.ControlPlane.Api-তে JWT signing key নিয়ে একটি CRITICAL কনফিগারেশন বাগ
আছে যা প্রোডাকশনে সম্পূর্ণ auth outage ঘটাবে।

সমস্যা ১ — কী ডাইভারজেন্স:
JWT signing key-র দুটি স্বতন্ত্র source of truth আছে।
- Program.cs (≈লাইন 107-119) — যা টোকেন *যাচাই* করে — Jwt:SecretKey এবং
  EDM_JWT_SECRET environment variable, দুটোই পড়ে।
- Services/TokenService.cs লাইন 34 — যা টোকেন *ইস্যু* করে — কেবল
  Jwt:SecretKey পড়ে, এবং না পেলে একটি hardcoded ডেভ কী-তে fallback করে।
Program.cs-এর exception message অপারেটরকে স্পষ্ট বলে EDM_JWT_SECRET সেট
করতে। অপারেটর তা করলে: টোকেন hardcoded ডেভ কী দিয়ে সাইন হবে কিন্তু শক্তিশালী
env কী দিয়ে যাচাই হবে → প্রতিটি লগইন সফল হবে, প্রতিটি টোকেন সাথে সাথেই
অকার্যকর হবে। ড্যাশবোর্ড সম্পূর্ণ অচল হবে এবং কারণ বের করা অত্যন্ত কঠিন।

যা করতে হবে:
১. একটি একক JWT অপশন ক্লাস (IOptions প্যাটার্ন) তৈরি করো যা DI-তে
   রেজিস্টার হবে, এবং Program.cs ও TokenService — দুজনেই কেবল সেটি পড়বে।
   কী রেজোলিউশন লজিক (env var + config) শুধু একটি জায়গায় থাকবে।
২. TokenService.cs লাইন 34-এর hardcoded fallback key সম্পূর্ণ সরাও।
৩. Program.cs লাইন 119-এর non-Production fallback key-ও সরাও। বর্তমানে
   Production গার্ডেড কিন্তু Staging/QA পাবলিক রিপোতে থাকা একটি hardcoded
   কী ব্যবহার করে, তাই যেকোনো Staging ইনস্ট্যান্সে নির্বিচারে টোকেন ফোর্জ
   করা সম্ভব। কী অনুপস্থিত থাকলে সব এনভায়রনমেন্টে স্টার্টআপে throw করো।
৪. স্টার্টআপে কী-র দৈর্ঘ্য যাচাই করো (≥ ৩২ বাইট) এবং একটি রানটাইম assertion
   যোগ করো যা নিশ্চিত করে issuing ও validating কী অভিন্ন।

সমস্যা ২ — হার্ডকোডেড seed admin (Program.cs ≈লাইন 220-231):
seeded SUPER_ADMIN-এর পাসওয়ার্ড সোর্সে লেখা, সাথে TwoFactorEnabled = false
এবং MustChangePassword = false। এটি পরিচিত ক্রেডেনশিয়ালে একটি স্থায়ী
SUPER_ADMIN তৈরি করে, এবং যেহেতু 2FA বন্ধ, তাই এটি password-reset চেইনের
সাথে মিলে সম্পূর্ণ টেকওভারের পথ খুলে দেয়।

যা করতে হবে:
৫. hardcoded পাসওয়ার্ড সরাও। env var না থাকলে একটি cryptographically random
   পাসওয়ার্ড জেনারেট করো, তা কেবল startup console-এ একবার প্রিন্ট করো
   (লগ ফাইলে নয়), এবং MustChangePassword = true সেট করো।
৬. seeded অ্যাডমিনের প্রথম লগইনে পাসওয়ার্ড পরিবর্তন ও 2FA এনরোলমেন্ট
   বাধ্যতামূলক করো — এই দুটি শেষ না হওয়া পর্যন্ত অন্য কোনো এন্ডপয়েন্টে
   অ্যাক্সেস দেবে না।
৭. setup-initial-admin এন্ডপয়েন্টটি একবার ব্যবহারের পর স্থায়ীভাবে
   disable হয়ে যায় কি না যাচাই করো; না হলে সেই গার্ড যোগ করো।

শেষে dotnet build + dotnet test চালাও এবং রিপোর্ট দাও।
```

---

# ফেজ ২ — ড্যাশবোর্ড

## P7 · Stored XSS চেইন বন্ধ করা

```text
EDM.ControlPlane.Dashboard-এ একটি CRITICAL unauthenticated-to-SUPER_ADMIN
stored XSS চেইন আছে। চারটি স্বতন্ত্র দুর্বলতা একসাথে কাজ করছে — চারটিই
ঠিক করতে হবে, একটিও বাদ দিলে চেইন খোলা থাকবে।

চেইনটি:
১. ইনজেকশন — POST /api/v1/support/tickets unauthenticated, CSRF-exempt এবং
   rate-limit-মুক্ত, তাই যে কেউ পেলোডসহ টিকিট জমা দিতে পারে।
২. এক্সিকিউশন — app.js-এ প্রায় ৫০টি জায়গায় সার্ভার-ডেটা কোনো escaping
   ছাড়াই innerHTML-এ বসানো হয়। নিশ্চিত করা sink: টিকিট লিস্ট ≈১২৯২, টিকিট
   মেসেজ ≈১৩৩৬, ইউজার ≈৫২০, ডিভাইস ≈৭০৮, লাইসেন্স ≈৭৬৬, রিলিজ ≈৯০৬ ও
   ≈১১১৫, অডিট লগ ≈১২৫৭, নোটিফিকেশন ≈১৪০৫, প্ল্যান ≈১৪৬৩, প্রাইসিং ≈১৫০৭,
   analytics by-country ≈১৬৮৪, এবং সব error path যেখানে ${err.message}
   ইন্টারপোলেট হয়।
৩. CSP — Middleware/SecurityHeadersMiddleware.cs-এর CSP-তে script-src-এ
   'unsafe-inline' আছে, তাই CSP ইনলাইন XSS ঠেকাবে না।
৪. টোকেন চুরি — api.js লাইন 371 JWT localStorage/sessionStorage থেকে পড়ে।
   সার্ভার HttpOnly কুকিও সেট করে, তাই web storage-এ রাখাটা সম্পূর্ণ
   অপ্রয়োজনীয় এবং HttpOnly-র পুরো উদ্দেশ্য নষ্ট করে।

গুরুত্বপূর্ণ: escapeHtml ফাংশনটি শুধু users.js লাইন ≈121-এ লোকালি ডিফাইন ও
৫টি জায়গায় ব্যবহৃত। app.js, devices.js, releases.js, sessions.js,
telemetry.js — সবগুলোতেই escaping নেই। এই আংশিক প্রতিরোধ রিভিউয়ারকে ভুল
আশ্বাস দেয়।

যা করতে হবে:

১. একটি শেয়ার্ড util ফাইল তৈরি করো যেখানে একটি escapeHtml (এবং attribute
   এস্কেপের জন্য একটি আলাদা ফাংশন) থাকবে। users.js-এর লোকাল কপি মুছে সেটি
   ব্যবহার করাও।
২. app.js, devices.js, releases.js, sessions.js, telemetry.js, users.js —
   প্রতিটি ফাইলে প্রতিটি innerHTML অ্যাসাইনমেন্ট অডিট করো। যেখানে
   ব্যবহারকারী/সার্ভার ডেটা ইন্টারপোলেট হচ্ছে, সেখানে হয় escape করো, নাহয়
   textContent / createElement / insertAdjacentText-এ রূপান্তর করো।
   টিকিট বডি ও মেসেজে (app.js ≈১২৯২, ≈১৩৩৬) অগ্রাধিকার দাও — এটাই
   unauthenticated ইনজেকশন পাথ।
   error path-এর ${err.message} / ${e.message} ইন্টারপোলেশনও escape করো।
৩. api.js থেকে localStorage/sessionStorage-এর JWT সম্পূর্ণ সরাও। কেবল
   credentials: "include" সহ HttpOnly কুকির উপর নির্ভর করো। auth.js-এ
   যেখানে টোকেন লেখা/পড়া হয় সেগুলোও পরিষ্কার করো।
৪. SecurityHeadersMiddleware.cs-এর CSP থেকে script-src ও style-src-এর
   'unsafe-inline' সরাও। ইনলাইন স্ক্রিপ্ট/স্টাইল বাইরের ফাইলে সরাও বা
   per-request nonce ব্যবহার করো। unpkg.com ও cdn.jsdelivr.net-এর স্ক্রিপ্ট
   self-host করো, নাহলে integrity (SRI) অ্যাট্রিবিউট যোগ করো।
   CSP-তে object-src 'none', base-uri 'self', form-action 'self' যোগ করো।
৫. একটি নতুন XSS sink যেন আর না ঢোকে — একটি lint rule বা CI grep চেক যোগ
   করো যা escape ছাড়া innerHTML টেমপ্লেট ইন্টারপোলেশন ধরলে ফেল করবে।

শেষে: একটি টিকিট বডিতে HTML পেলোড দিয়ে ম্যানুয়ালি যাচাই করো যে তা টেক্সট
হিসেবে রেন্ডার হচ্ছে, স্ক্রিপ্ট হিসেবে নয়। রিপোর্ট দাও।
```

## P8 · ড্যাশবোর্ড আর্কিটেকচার ও প্রোডাকশন হাইজিন

```text
EDM.ControlPlane.Dashboard-এ কয়েকটি আর্কিটেকচারাল ও হাইজিন সমস্যা ঠিক করো।

১. HIGH — api.js-এর EDM_API_CONFIG-এ REQUEST_TIMEOUT_MS = 15000। রিলিজ
   আর্টিফ্যাক্ট আপলোড ৩৪ MB, যা ১৫ সেকেন্ডে অসম্ভব — AbortController আপলোড
   মাঝপথে কেটে দেবে।
   → per-request timeout override যোগ করো; আপলোড/ডাউনলোড কলে অনেক বড়
     timeout (বা কোনো timeout নয়) দাও, এবং আপলোডে progress রিপোর্টিং যোগ করো।

২. MEDIUM — mock-data.js (২২৮ লাইন কাল্পনিক ইউজার, IP, ডিভাইস ডেটা)
   প্রোডাকশন অ্যাসেটের পাশে শিপ হচ্ছে।
   → প্রোডাকশন বিল্ড/ডিপ্লয়মেন্ট থেকে সম্পূর্ণ বাদ দাও। যদি ডেভ-টাইমে দরকার
     হয়, একটি স্পষ্ট dev-only ফ্ল্যাগের পিছনে রাখো।

৩. MEDIUM — কোনো কেন্দ্রীয় state store নেই; প্রতিটি ভিউ নিজে ফেচ করে সরাসরি
   innerHTML লেখে, তাই ট্যাব বদলালেই সম্পূর্ণ রিফেচ হয়, এবং caching বা
   optimistic update অসম্ভব।
   → একটি হালকা কেন্দ্রীয় store মডিউল যোগ করো (একটি সাধারণ observable
     অবজেক্টই যথেষ্ট, নতুন ফ্রেমওয়ার্ক আনার দরকার নেই) — fetch → store →
     render প্যাটার্ন, TTL-ভিত্তিক ক্যাশ সহ। app.js (১৭৩২ লাইন) কে
     ভিউ-প্রতি মডিউলে ভাগ করো।

৪. LOW — api.js-এর initCsrfToken() কনস্ট্রাক্টরে await ছাড়াই fire-and-forget
   কল হয় এবং ব্যর্থতা নীরবে গিলে ফেলে।
   → একটি promise রাখো যা প্রথম mutating রিকোয়েস্ট await করবে; ব্যর্থতা লগ করো।

৫. ক্লায়েন্ট-সাইড role gating (auth.js) নিরাপত্তা সীমা হিসেবে ব্যবহার করা
   যাবে না। প্রতিটি API এন্ডপয়েন্টে সার্ভার-সাইড [Authorize] পলিসি আছে কি না
   অডিট করো এবং যেগুলোতে নেই তার একটি তালিকা রিপোর্টে দাও (এখনই ঠিক করার
   দরকার নেই, শুধু তালিকা)।

৬. api.js লাইন 116 ও 223, app.js ≈327, devices.js ≈23-এ hardcoded ভার্সন
   fallback আছে ("v2.1.0"/"2.0.0")। এখন শুধু সেগুলো একটি single constant-এ
   একত্র করো — প্রকৃত মান P16-এ ঠিক হবে।

শেষে ব্রাউজারে ড্যাশবোর্ড লোড করে সব ট্যাব যাচাই করো এবং রিপোর্ট দাও।
```

---

# ফেজ ৩ — ইঞ্জিন ডেটা ইন্টিগ্রিটি

## P9 · রিট্রাই লুপ ও ওয়ার্ক-স্টিলিং করাপশন

```text
EDM ডেস্কটপ ডাউনলোড ইঞ্জিনে তিনটি HIGH severity concurrency/ইন্টিগ্রিটি বাগ
আছে। তিনটিরই মূল কারণ একটি ভাগ করা ডিজাইন সমস্যা: SegmentScheduler ক্লোন
রিটার্ন করে কিন্তু কলার সেই ক্লোনে লেখে।

১. অসীম রিট্রাই লুপ:
   EDM/Services/SegmentScheduler.cs-এর GetNextWorkItem (লাইন 125-183)
   pending.Clone() এবং newSegment.Clone() রিটার্ন করে। কিন্তু
   EDM/Services/MultiPartDownloader.cs (≈লাইন 379) ব্যর্থতায়
   segment.RetryCount++ করে — সেটি ক্লোনের উপর, যা পরের ইটারেশনেই ফেলে দেওয়া
   হয়। শিডিউলারের আসল অবজেক্টে RetryCount কখনো লেখা হয় না, এবং
   MarkFailed(requeue: true) স্টেট Pending-এ রিসেট করে কিন্তু RetryCount
   সংরক্ষণ করে না। ফলে `RetryCount <= 8` গার্ডটি কখনোই পূরণ হয় না।
   প্রভাব: কোনো সেগমেন্ট যদি ধারাবাহিকভাবে ব্যর্থ হয় (যেমন মাঝপথে signed
   URL এক্সপায়ার), ডাউনলোড অনির্দিষ্টকাল ঘুরতে থাকে — ব্যবহারকারীকে কোনো
   error দেখায় না, বন্ধও হয় না।
   → RetryCount শিডিউলারের ভেতরে রাখো। MarkFailed-এ একটি retryCount
     ইনক্রিমেন্ট প্যারামিটার/লজিক যোগ করো। সর্বোচ্চ retry ছাড়ালে সেগমেন্ট
     Failed করো এবং সম্পূর্ণ ডাউনলোড একটি স্পষ্ট error সহ terminate করো।
     Retry delay-এ exponential backoff + jitter দাও (বর্তমানে ≤১ সেকেন্ড ফ্ল্যাট)।

২. ওয়ার্ক-স্টিলিং সেগমেন্টকে অকালে Completed করে → নীরব ফাইল করাপশন:
   GetNextWorkItem (লাইন 161) ভিকটিম সেগমেন্টের End ছোট করে দেয় যখন ভিকটিম
   ওয়ার্কার এখনো স্ট্রিম করছে। এদিকে ReportProgress (লাইন 194-208) নতুন ছোট
   TotalBytes-এর বিপরীতে clamp করে এবং সাথে সাথেই State = Completed করে
   দেয়, যদিও প্রকৃত বাইট কম লেখা হয়েছে। MarkCompleted (লাইন 223-237) তখন
   BytesDownloaded = TotalBytes বসিয়ে দেয়। মার্জে সেই খাটো .part ফাইল জোড়া
   লাগে এবং পরবর্তী সব বাইট শিফট হয়ে যায় — নীরব দুর্নীতি, কোনো error নেই।
   → ReportProgress থেকে auto-completion লজিক সম্পূর্ণ সরাও। Completion
     কেবল ওয়ার্কারের explicit MarkCompleted কলে হবে, এবং MarkCompleted-এ
     actual bytes written প্যারামিটার নাও ও তা expected-এর সাথে না মিললে
     false রিটার্ন করে সেগমেন্ট Pending করো — নীরবে TotalBytes বসাবে না।
   → boundary shrink করার সময় ভিকটিমের বর্তমান লেখা bytes অবশ্যই respect
     করো; কোনোভাবেই already-written অংশের চেয়ে ছোট End সেট করবে না।

৩. শেয়ার্ড mutable metaState ৩২ থ্রেড থেকে সিঙ্ক্রোনাইজেশন ছাড়া মিউটেট হয়:
   EDM/Services/SegmentWorker.cs লাইন 198 ও 229, এবং MultiPartDownloader.cs
   (≈লাইন 320) — প্রত্যেকে একই metaState অবজেক্টে .Segments অ্যাসাইন করে,
   এবং একই সময়ে অন্য থ্রেড সেটি JSON-এ সিরিয়ালাইজ করতে পারে। কোনো লক নেই →
   InvalidOperationException বা ছেঁড়া (torn) মেটাডেটা, যা resume state নষ্ট করে।
   → metaState-কে immutable snapshot মডেলে সরাও: ওয়ার্কার কখনো শেয়ার্ড
     অবজেক্টে লিখবে না। একটি একক owner (persistence coordinator) scheduler
     থেকে snapshot নিয়ে লিখবে। ওয়ার্কার শুধু scheduler-এ progress রিপোর্ট করবে।

শেষে dotnet build + dotnet test চালাও। একটি টেস্ট লেখো যা প্রমাণ করে
ধারাবাহিকভাবে ব্যর্থ সেগমেন্ট নির্দিষ্ট retry-র পর ডাউনলোড fail করায়,
এবং আরেকটি যা প্রমাণ করে work-stealing-এর পর কোনো সেগমেন্ট short file নিয়ে
Completed হয় না। রিপোর্ট দাও।
```

## P10 · মার্জ, ভেরিফিকেশন, ডিস্ক স্পেস ও রিজিউম হ্যাশ

```text
EDM ডেস্কটপ ইঞ্জিনের file assembly ও resume validation পাথে চারটি HIGH
severity ইন্টিগ্রিটি বাগ আছে।

১. মার্জ অনুপস্থিত সেগমেন্ট নীরবে বাদ দেয়:
   EDM/Services/MultiPartDownloader.cs-এর MergeFilesAsync (≈লাইন 593)
   `if (File.Exists(chunk))` চেক করে — .part ফাইল না থাকলে কোনো error ছাড়াই
   এড়িয়ে যায়। প্রতিটি অংশের দৈর্ঘ্য expected দৈর্ঘ্যের সাথে মেলানোও হয় না।
   → অনুপস্থিত .part ফাইলে throw করো। প্রতিটি অংশের actual length বনাম
     expected segment length যাচাই করো; না মিললে throw করো। মার্জের আগে
     সব অংশের মোট আকার totalBytes-এর সমান কি না assert করো।

২. ভেরিফিকেশন ব্যর্থতা বিভ্রান্তিকর FileNotFoundException হিসেবে প্রকাশ পায়:
   MultiPartDownloader.cs (≈লাইন 610-620) — VerificationFailed হলে এবং
   expectedHash খালি থাকলে (যা সর্বদাই খালি, কারণ caller তা পাঠায় না) কোড
   tempMergePath ডিলিট করে কিন্তু throw করে না, তারপর সেই মুছে ফেলা ফাইলেই
   File.Move চালায়। ফলে data corruption একটি অপ্রাসঙ্গিক
   FileNotFoundException হয়ে আসে এবং মূল কারণ সম্পূর্ণ ঢাকা পড়ে।
   → verification ব্যর্থ হলে সাথে সাথেই একটি স্পষ্ট integrity exception
     throw করো এবং File.Move-এ কখনো পৌঁছাবে না। expectedHash খালি থাকলে
     status VerificationUnavailable দাও (VerificationFailed নয়) এবং তা
     history/UI-তে স্পষ্টভাবে দেখাও।

৩. ৩× ডিস্ক স্পেস কিন্তু ১× প্রি-চেক:
   MultiPartDownloader.cs (≈লাইন 248) destination ফাইল SetLength(totalBytes)
   দিয়ে প্রি-অ্যালোকেট করে (১×), তার উপর সব .part ফাইল (১×), তার উপর
   .merging ফাইল (১×) — পিক ডিস্ক ব্যবহার ৩×। কিন্তু
   DiskSpaceGovernor.EnsureAvailableSpaceOrThrow শুধু ১× চেক করে। ফলে ১২ GB
   ফ্রি ডিস্কে ১০ GB ডাউনলোড সম্পূর্ণ হয়ে মার্জের সময় disk-full হয়ে সব
   কাজ নষ্ট হবে।
   → এখনই: DiskSpaceGovernor-এর চেক প্রকৃত peak requirement-এর সাথে মিলাও।
   → এবং destination ফাইলের নিরর্থক প্রি-অ্যালোকেশন সরাও — শেষে File.Move
     সেই ফাইলটিই প্রতিস্থাপন করে, তাই এটি কোনো fragmentation সুবিধা দেয় না,
     শুধু ১× অতিরিক্ত ডিস্ক ও একটি বড় sparse write খরচ করে।
   (নোট: P13-এ merge pass সম্পূর্ণ বিলুপ্ত হবে, যা এই সমস্যার স্থায়ী সমাধান।
    এই প্রম্পটে শুধু সঠিক চেক ও নিরর্থক প্রি-অ্যালোকেশন সরাও।)

৪. রিজিউমে incremental SHA-256 ভুল হয়:
   EDM/Services/SegmentWorker.cs লাইন 105-এ IncrementalHash শূন্য থেকে শুরু
   হয়, কিন্তু লাইন 100-এ fs.Seek(segment.BytesDownloaded) ইতোমধ্যে ডাউনলোড
   হওয়া বাইট skip করে। ফলে হ্যাশে কেবল নতুন-ফেচ করা অংশ যায়, অথচ লাইন 208-এ
   সেটিই segment.Sha256Hash হিসেবে সংরক্ষিত হয়। অর্থাৎ resume করা যেকোনো
   ডাউনলোডের সব segment hash অর্থহীন, এবং বিজ্ঞাপিত per-segment integrity
   verification কার্যত অকার্যকর।
   এর সরাসরি পরিণতি EDM/Services/DurableMetadataManager.cs-এ:
   ReconcileAndValidate (লাইন 334-357) `seg.BytesDownloaded = actualLen`
   অর্থাৎ ফাইলের দৈর্ঘ্যকেই সত্য ধরে নেয়, এবং Sha256Hash null হলে (যা এই
   বাগের কারণে সাধারণ ঘটনা) সেগমেন্ট কোনো যাচাই ছাড়াই Completed ঘোষিত হয়।
   ক্র্যাশে NTFS sparse hole থাকলেও দৈর্ঘ্য ঠিক থাকবে → corruption বৈধ
   হিসেবে গৃহীত।
   → resume করার সময় বিদ্যমান বাইটগুলো hasher-এ feed করো (ফাইলটি প্রথমে
     BytesDownloaded পর্যন্ত পড়ে hash-এ দাও), তারপর নতুন ডেটা append করো।
   → ReconcileAndValidate-এ Sha256Hash null থাকলে সেগমেন্টকে Completed
     ঘোষণা করবে না — হয় hash কম্পিউট করো, নাহয় সেগমেন্ট Pending রাখো।

শেষে dotnet build + dotnet test। একটি টেস্ট লেখো যা resume-এর পর সম্পূর্ণ
সেগমেন্টের hash সঠিক প্রমাণ করে, এবং আরেকটি যা .part ফাইল মুছে দিলে merge
throw করে তা প্রমাণ করে। রিপোর্ট দাও।
```

## P11 · Pause/Resume ডেডলক

```text
EDM/Services/MultiPartDownloader.cs-এ একটি HIGH severity lost-wakeup race
আছে যা স্থায়ী ডেডলক ঘটায়।

সমস্যা (Pause()/Resume(), ≈লাইন 84-101):
Resume() প্রথমে TCS-এ TrySetResult করে, তারপর _resumeTcs = null করে। এই দুই
ধাপের মাঝে যদি Pause() চলে এবং একটি নতুন TCS অ্যাসাইন করে, তবে Resume()-এর
শেষ লাইনটি সেই নতুন pause-এর TCS-কেই মুছে দেয়। পরবর্তী Resume() তখন null-এ
TrySetResult করে — অর্থাৎ কিছুই হয় না। যে ওয়ার্কাররা সেই orphan TCS-এ await
করছিল, তারা চিরকাল আটকে থাকে। ডাউনলোড হ্যাং করে, CPU শূন্য, কোনো error নেই।
এছাড়া _isPaused volatile কিন্তু _resumeTcs নয়, তাই publication ordering-ও
অসুরক্ষিত।

গুরুত্বপূর্ণ: রিপোতে ইতোমধ্যেই একটি সঠিক অ্যাবস্ট্রাকশন আছে —
EDM/Services/PauseTokenSource.cs — কিন্তু এই কোড তা ব্যবহার করে না।

আরও দুটি সম্পর্কিত সমস্যা একই ফাইলে:

১. workerTasks লিস্ট append-only, কখনো prune হয় না (≈লাইন 470)। প্রস্থান-করা
   ওয়ার্কারও গণনায় থাকে, তাই workerTasks.Count জীবিত ওয়ার্কারের প্রতিনিধিত্ব
   করে না। `while (workerTasks.Count < evaluatedCount)` শর্তের কারণে একবার
   count পৌঁছে গেলে adaptive scale-up আর কখনো হবে না — অর্থাৎ ফ্ল্যাগশিপ
   "adaptive connection scaling" ফিচারটি এক-দিকমুখী হয়ে তারপর স্থবির।
   এছাড়া controlled worker removal-এর break (≈লাইন 301) জীবিত ওয়ার্কার
   শূন্যে নামিয়ে দিতে পারে যখন সেগমেন্ট এখনো pending।
২. accountant.OnWorkerBusy() ওয়ার্কার প্রস্থানের finally ব্লকে কল হয়
   (≈লাইন 396) — এখানে OnWorkerIdle() বা decrement হওয়া উচিত। এটি adaptive
   controller-কে ভুল occupancy telemetry দিচ্ছে।

যা করতে হবে:
১. হাতে লেখা _isPaused / _resumeTcs লজিক সম্পূর্ণ সরিয়ে বিদ্যমান
   PauseTokenSource ব্যবহার করো। Pause/Resume idempotent হবে এবং যেকোনো
   ক্রমে/গতিতে কল করলেও কোনো waiter হারাবে না।
২. workerTasks-কে জীবিত ওয়ার্কারের সঠিক প্রতিনিধিত্বে রূপান্তর করো —
   সম্পন্ন task prune করো, যাতে scale-up ও scale-down দুইদিকেই কাজ করে।
   জীবিত ওয়ার্কার শূন্য হওয়া প্রতিরোধ করো যতক্ষণ pending সেগমেন্ট আছে।
৩. OnWorkerBusy() → সঠিক idle/decrement কলে বদলাও।

শেষে একটি stress test লেখো: ১০ MB+ ডাউনলোড চলাকালীন ৫০ বার এলোমেলো
বিরতিতে (১-২০ ms) Pause/Resume টগল করো এবং প্রমাণ করো ডাউনলোড সম্পূর্ণ হয়
ও হ্যাং করে না। docs/KNOWN_ISSUES.md-এর "Pause/Resume Toggles 10 Cycles
@ 100ms — PASS (No Deadlock)" দাবিটি এই বাগ ধরতে ব্যর্থ হয়েছিল, তাই টেস্টটি
আরও আগ্রাসী হতে হবে। রিপোর্ট দাও।
```

---

# ফেজ ৪ — পারফরম্যান্স (IDM-কে হারানোর মূল কাজ)

## P12 · মেটাডেটা পার্সিস্টেন্স — একক বৃহত্তম গতি লাভ

```text
EDM ডাউনলোড ইঞ্জিনের প্রধান throughput bottleneck মেটাডেটা পার্সিস্টেন্সে।
এটি ঠিক করলে গতি সবচেয়ে বেশি বাড়বে। খুব সতর্কতার সাথে করো — crash-safety
নষ্ট করা যাবে না।

সমস্যা:
EDM/Services/SegmentWorker.cs লাইন 196 প্রতি ২৫৬ KB-তে WriteStateAtomicAsync
কল করে। কিন্তু read buffer-ও ২৫৬ KB (লাইন 102), তাই এটি কার্যত প্রতিটি read
iteration-এ একবার।

আর প্রতিটি কলের খরচ (EDM/Services/DurableMetadataManager.cs লাইন 79-126):
- CaptureSnapshot — সব সেগমেন্টের deep clone (লাইন 153)
- JsonSerializer.Serialize with WriteIndented = true (লাইন 67) — pretty-printed,
  ২-৩× বড় ও ধীর
- lock (_writeLock) (লাইন 99) — একটি async মেথডের ভেতরে blocking lock, যা
  সম্পূর্ণ disk I/O জুড়ে ধরে রাখা হয় → ৩২ ওয়ার্কারে thread-pool starvation
- FileOptions.WriteThrough (লাইন 102) + fs.Flush(flushToDisk: true) (লাইন 107)
  — প্রতিবার একটি physical FlushFileBuffers
- File.Copy(metaPath, bakPath, overwrite: true) (লাইন 113) — প্রতিবার
  সম্পূর্ণ মেটাডেটা ফাইলের অতিরিক্ত read+write
- File.Move(overwrite: true) (লাইন 117)

পরিণতি: HDD-তে প্রতি flush ~১০ ms → পুরো অ্যাপে সর্বোচ্চ ~১০০ মেটাডেটা
write/সেকেন্ড → aggregate throughput ~২৫ MB/s-এ hard-capped। এবং global
lock-এর কারণে connection সংখ্যা বাড়ালে গতি বাড়ে না, বরং contention বাড়ে —
অর্থাৎ "৩২-সকেট turbo" ফিচারটি নিজের বিরুদ্ধেই কাজ করছে।

যা করতে হবে:

১. byte-count trigger সম্পূর্ণ সরাও। SegmentWorker আর সরাসরি
   WriteStateAtomicAsync কল করবে না।
২. একটি একক persistence coordinator তৈরি করো যা time-debounced ভাবে লেখে
   (২-৫ সেকেন্ড অন্তর, configurable), scheduler থেকে snapshot নিয়ে।
   একাধিক pending request একত্রে coalesce হবে (একটি pending flag/channel)।
৩. WriteIndented = false করো। প্রয়োজনে diagnostics-এর জন্য আলাদা একটি
   indented export মেথড রাখো।
৪. lock (_writeLock) → SemaphoreSlim + WaitAsync, যাতে thread block না হয়।
৫. প্রতিটি write-এ File.Copy সরাও। .bak কেবল checkpoint-এ তৈরি করো
   (যেমন প্রতি Nতম write বা প্রতি ৩০ সেকেন্ডে)। এবং লাইন 113-এর
   `catch { }` সরাও — .bak কপি ব্যর্থ হলে অন্তত warning লগ করো, কারণ এখন
   backup নীরবে বহু generation পুরনো থাকতে পারে।
৬. WriteThrough + flushToDisk শুধু checkpoint write-এ রাখো, প্রতিটি
   incremental write-এ নয়। Pause, cancel, ডাউনলোড সম্পন্ন, এবং অ্যাপ
   shutdown-এ অবশ্যই একটি flushed checkpoint লেখো।
৭. ComputeSegmentHash (লাইন 376) একটি synchronous full-file read এবং
   ReconcileAndValidate থেকে calling path-এ কল হয় — ১০ GB resume করলে অ্যাপ
   কয়েক মিনিট সম্পূর্ণ অসাড় দেখাবে। এটি async করো, ব্যাকগ্রাউন্ডে চালাও,
   এবং UI-তে "verifying resume state" progress দেখাও।
৮. CleanOrphanTempDirectories (লাইন 391) ৭ দিনের threshold ব্যবহার করে,
   তাই orphan .tmp_* ডিরেক্টরি (প্রতিটিতে সম্ভবত পূর্ণ ফাইল কপি) এক সপ্তাহ
   ডিস্ক ধরে রাখে। threshold configurable করো এবং ডিফল্ট অনেক কমাও (যেমন
   ২৪ ঘণ্টা), সাথে অ্যাপ স্টার্টআপে একবার চালাও।

crash-safety নিশ্চিত করতে: একটি টেস্ট লেখো যা ডাউনলোড মাঝপথে প্রক্রিয়া
kill করে (বা kill সিমুলেট করে) এবং প্রমাণ করে resume সঠিকভাবে কাজ করে।

শেষে একটি before/after throughput বেঞ্চমার্ক চালাও (একই ফাইল, একই
connection count) এবং সংখ্যা সহ রিপোর্ট দাও।
```

## P13 · মার্জ পাস বিলুপ্ত করা — IDM-এর ডিস্ক আর্কিটেকচারে যাওয়া

```text
এটি একটি বড় আর্কিটেকচারাল পরিবর্তন। আলাদা ব্রাঞ্চে করো। P10 ও P12 শেষ
হওয়ার পর শুরু করো।

সমস্যা:
EDM বর্তমানে প্রতিটি সেগমেন্ট একটি আলাদা .part ফাইলে লেখে
(EDM/Services/SegmentWorker.cs লাইন 92-98), তারপর MultiPartDownloader.cs-এর
MergeFilesAsync সব .part একটি .merging ফাইলে জোড়া দেয়, তারপর File.Move
করে। এর খরচ:
- পিক ডিস্ক ব্যবহার ৩× ফাইল সাইজ
- প্রতিটি বাইট দুবার লেখা ও একবার পড়া হয়
- একটি সম্পূর্ণ অতিরিক্ত sequential I/O pass, যা বড় ফাইলে মিনিটে গুনতে হয়
- MergeFilesAsync-এর সব ইন্টিগ্রিটি বাগের উৎস

IDM এই কাজটি ভিন্নভাবে করে: সেগমেন্টগুলো সরাসরি একটি প্রি-অ্যালোকেটেড
destination ফাইলের নিজস্ব offset-এ লেখে। কোনো .part ফাইল নেই, কোনো merge
pass নেই, ১× ডিস্ক, ১× write। এটিই ডিস্ক I/O-তে IDM-এর সাপেক্ষে EDM-এর
সবচেয়ে বড় আর্কিটেকচারাল ফাঁক।

যা করতে হবে:

১. SegmentWorker-কে .part ফাইলের বদলে একটি শেয়ার্ড destination ফাইলে
   লেখাও। প্রতিটি ওয়ার্কার FileShare.ReadWrite সহ নিজের FileStream খুলবে
   এবং তার নিজস্ব absolute offset-এ লিখবে (segment.Start + BytesDownloaded)।
   Overlapping write যাতে কখনো না হয় তা scheduler boundary দিয়ে নিশ্চিত করো।
২. ডাউনলোড শুরুতে destination ফাইল একবার SetLength(totalBytes) দিয়ে
   প্রি-অ্যালোকেট করো (এখন এটি প্রকৃত অর্থে প্রয়োজনীয় ও কার্যকর, কারণ আর
   কোনো File.Move প্রতিস্থাপন হবে না)। partial ফাইলটি একটি স্পষ্ট
   in-progress এক্সটেনশনে রাখো (যেমন .edmdownload) এবং সম্পূর্ণ ও verified
   হলে একবার rename করো।
৩. MergeFilesAsync সম্পূর্ণ সরাও, .merging ফাইল সরাও, এবং সব .part path
   হ্যান্ডলিং সরাও।
৪. DurableMetadataManager.ReconcileAndValidate-এর resume validation
   .part-ফাইল-দৈর্ঘ্য মডেল থেকে সরিয়ে offset-range মডেলে নাও: প্রতিটি
   সেগমেন্টের BytesDownloaded মেটাডেটা থেকে আসবে, এবং verification
   destination ফাইলের সেই range পড়ে hash মিলিয়ে হবে।
   সতর্কতা: এখন আর "ফাইল আছে কি না / দৈর্ঘ্য কত" দিয়ে যাচাই করা যাবে না,
   কারণ ফাইলটি সবসময় পূর্ণ আকারে থাকবে। তাই metadata-ই একমাত্র authority
   এবং P12-এর checkpoint flush নির্ভরযোগ্য হতে হবে।
৫. DiskSpaceGovernor-এর requirement ১× + সামান্য margin-এ নামাও।
৬. চূড়ান্ত whole-file verification যোগ করো: সব সেগমেন্ট সম্পন্ন হলে
   destination ফাইলের একটি সম্পূর্ণ hash কম্পিউট করো এবং সার্ভার-প্রদত্ত
   checksum (Digest বা Content-MD5 হেডার) থাকলে তার সাথে মেলাও।

শেষে যাচাই করো: (ক) ডাউনলোড করা ফাইল byte-for-byte সঠিক, (খ) পিক ডিস্ক
ব্যবহার ফাইল সাইজের ~১×, (গ) resume কাজ করে, (ঘ) work-stealing এখনো কাজ
করে। before/after সময় ও ডিস্ক ব্যবহারের সংখ্যা সহ রিপোর্ট দাও।
```

## P14 · CPU হটস্পট ও লক কনটেনশন

```text
EDM ডাউনলোড ইঞ্জিনে তিনটি CPU হটস্পট আছে যা উচ্চ connection count-এ গতি
কমিয়ে দেয়।

১. গ্লোবাল লকের নিচে সেকেন্ডে ~১২,৮০০ LINQ স্ক্যান:
   EDM/Services/SegmentScheduler.cs লাইন 185-192-এর GetAssignedEnd একটি
   global lock-এর ভেতরে O(n) FirstOrDefault চালায় (প্রতিবার একটি delegate
   allocation সহ), এবং SegmentWorker.cs লাইন 135 এটি প্রতিটি ওয়ার্কারের
   প্রতি ২৫৬ KB read-এ কল করে। ৩২ ওয়ার্কার × ~৪০০ read/সেকেন্ড = একটিমাত্র
   লকে সেকেন্ডে ~১২,৮০০ acquisition।
   একই সমস্যা ReportProgress (লাইন 194), UpdateTempPath (লাইন 210),
   MarkCompleted (লাইন 223), MarkFailed (লাইন 239) — সবাই FirstOrDefault।
   আর GetSegmentsSnapshot (লাইন 328) আরও খারাপ: এটি লকের ভেতরে সব সেগমেন্টের
   deep clone + OrderBy করে, এবং worker loop, প্রতিটি segment acquisition,
   প্রতিটি metadata write, ১০০ ms telemetry loop — সব জায়গা থেকে কল হয়।
   → _segments List<SegmentRange> এর পাশে একটি Dictionary<int, SegmentRange>
     index রাখো যাতে সব lookup O(1) হয়।
   → GetAssignedEnd হট পাথ থেকে সরাও: boundary-কে একটি per-segment
     volatile/Interlocked ফিল্ডে রাখো যা ওয়ার্কার লক ছাড়াই পড়তে পারবে।
   → GetSegmentsSnapshot-এর কলগুলো হট পাথ থেকে সরাও (P12-এর debounced
     coordinator-এ একত্র করো), এবং telemetry-র জন্য একটি হালকা aggregate
     (মোট bytes, state count) রাখো যা সম্পূর্ণ clone ছাড়াই পাওয়া যাবে।

২. প্রতি read-এ নতুন linked CancellationTokenSource + Timer:
   SegmentWorker.cs লাইন 118-119 — read loop-এর ভেতরে
   CreateLinkedTokenSource + CancelAfter। প্রতিটি linked CTS parent token-এ
   একটি callback register করে (একটি locked list) এবং একটি Timer তৈরি করে।
   ৩২ ওয়ার্কারে সেকেন্ডে ~১২,৮০০ CTS+Timer allocation/disposal — একটি
   সুপরিচিত .NET anti-pattern ও বড় CPU হটস্পট।
   → ওয়ার্কার-প্রতি একটি CTS তৈরি করো এবং প্রতিটি read-এর আগে
     CancelAfter দিয়ে deadline রিসেট করো, অথবা ReadAsync-এ একটি
     Task.WhenAny/timeout প্যাটার্ন ব্যবহার করো যা প্রতি iteration-এ
     allocate করে না।

৩. UI flooding:
   MultiPartDownloader.cs-এর ১০০ ms telemetry loop (≈লাইন 411) প্রতিবার
   সম্পূর্ণ snapshot + ৩টি LINQ Count() + progress?.Report করে, এবং
   lastSegSamples প্রতিটি segment id-র জন্য বাড়তে থাকে ও chunkStatsMap
   কখনো clear হয় না (মেমরি লিক)।
   আর DownloadSingleAsync প্রতিটি ১২৮ KB বাফারে progress?.Report করে —
   ১০০ MB/s-এ সেকেন্ডে ~৮০০ dispatcher post, যা UI জমিয়ে দেয়।
   → সব progress reporting একটি coalescing layer-এর পিছনে নাও, সর্বোচ্চ
     ৪-৫ Hz UI আপডেট। DownloadSingleAsync-এর per-buffer report সরাও।
   → lastSegSamples ও chunkStatsMap-এ completed/removed segment-এর এন্ট্রি
     evict করো। SegmentScheduler-এর _workerTelemetry ডিকশনারিও (লাইন 100)
     কখনো evict হয় না — সেটিও ঠিক করো।

৪. MultiPartDownloader.cs-এর DownloadSingleAsync-এ (≈লাইন 659) throttle
   `Task.Delay((int)Math.Max(1, delayMs))` ব্যবহার করে। Windows-এ timer
   granularity ~১৫.৬ ms, তাই উচ্চ লিমিটে গণিত ~১.৩ ms চাইলেও বাস্তবে
   ≥১৫ ms ঘুমায় → throughput ~৮.৫ MB/s-এ আটকে যায়, লিমিট যত বড়ই হোক।
   অর্থাৎ *বেশি* লিমিট দিলে গতি *কমে*।
   → এই hand-rolled throttle সম্পূর্ণ সরাও (P15-এ unified throttler ব্যবহার
     হবে)। অন্তত token-bucket ভিত্তিক করো যাতে ছোট delay জমিয়ে একবারে নেওয়া হয়।

শেষে একটি CPU profile before/after নাও (৩২ connection, বড় ফাইল) এবং
হটস্পট তালিকা সহ রিপোর্ট দাও।
```

## P15 · HTTP লেয়ার একীভূতকরণ ও নেটওয়ার্ক নির্ভরযোগ্যতা

```text
EDM-এর HTTP লেয়ারে ৯টি বাগ আছে, যার কয়েকটি IDM-এর তুলনায় সরাসরি
প্রতিযোগিতামূলক পরাজয় ঘটাচ্ছে। এছাড়া দুটি প্রতিদ্বন্দ্বী HTTP abstraction
একসাথে চলছে।

ফাইল: EDM/Services/SharedHttpClient.cs এবং EDM/Services/HttpClientProvider.cs

১. HIGH — client.Timeout = TimeSpan.FromMinutes(10) সম্পূর্ণ অপারেশনে
   প্রযোজ্য, body streaming সহ (ResponseHeadersRead ব্যবহার করলেও)। ২০০ KB/s
   লাইনে ২৫৬ MB সেগমেন্টে ~২১ মিনিট লাগে → নিশ্চিত ব্যর্থতা। IDM-এ এমন
   কোনো সীমা নেই। সঠিক মেকানিজম ইতোমধ্যেই আছে — SegmentWorker.cs লাইন 19-এর
   ৩০ সেকেন্ড per-read inactivity timeout।
   → Timeout = Timeout.InfiniteTimeSpan করো এবং per-read timeout-কেই
     একমাত্র মেকানিজম রাখো।

২. MEDIUM — HEAD-নির্ভর capability probing:
   MultiPartDownloader.cs (≈লাইন 141) HEAD request-এর উপর নির্ভর করে। বহু
   CDN HEAD-এ ৪০৫/৪০৩ দেয় বা Content-Length দেয় না। কোড কেবল Accept-Ranges
   *অনুপস্থিত* থাকলে range probe করে; HEAD নিজেই ব্যর্থ হলে কোনো GET চেষ্টা
   না করেই পুরো ডাউনলোড ব্যর্থ হয়। IDM `Range: bytes=0-` সহ GET পাঠায়, যা
   প্রায় সর্বত্র কাজ করে।
   → primary probe হবে `Range: bytes=0-` সহ একটি GET (ResponseHeadersRead,
     তারপর সাথে সাথে dispose)। HEAD শুধু fallback। ২০৬ ও Content-Range
     থেকে total size বের করো।

৩. HIGH — SendWithRetryAsync একই HttpRequestMessage ইনস্ট্যান্স পুনঃপ্রেরণ
   করে (কোডে কমেন্টে ইচ্ছাকৃত বলা হয়েছে, "লিক এড়াতে")। .NET এতে
   InvalidOperationException ছোড়ে, সেই exception একই catch filter-এ ধরা পড়ে,
   তিনটি attempt পুড়ে যায়, এবং শেষে মূল network error-এর বদলে একটি
   বিভ্রান্তিকর exception প্রকাশ পায় — অর্থাৎ retry সম্পূর্ণ non-functional।
   → SendWithRetryAsync সরাও। রিপোতে সঠিক প্যাটার্ন ইতোমধ্যেই আছে —
     HttpRequestPipeline-এর requestFactory-ভিত্তিক ExecuteWithRetryAsync
     (SegmentWorker.cs লাইন 62-71 এটি ব্যবহার করছে)। সব কলার সেটিতে সরাও।

৪. MEDIUM — MaxConnectionsPerServer = Math.Max(64, maxConnections):
   switch যত্ন করে Cellular-এ ৮, MeteredNetwork-এ ৮, MobileHotspot-এ ১২
   নির্ধারণ করে, তারপর Math.Max(64, ...) সব নেটওয়ার্ক টাইপে ৬৪-এর floor
   চাপিয়ে দেয়। ফলে সম্পূর্ণ adaptive per-network limiting dead code, এবং
   metered/cellular ব্যবহারকারী ৬৪ socket পায় — ডেটা ক্যাপ ক্ষতি ও
   congestion collapse। docs/KNOWN_ISSUES.md-এর "steps down per metered
   network type" দাবিটি এই এক লাইনেই মিথ্যা প্রমাণিত।
   → Math.Max(64, ...) সরাও, switch-এর মান সম্মান করো।

৫. HIGH — ApplyProxySettings / RebuildForNetworkChange পুরনো HttpClient
   disposeHandler: true সহ ৫ সেকেন্ড grace পরে dispose করে, যা
   SocketsHttpHandler ও তার সব pooled connection বন্ধ করে দেয় — অর্থাৎ সব
   in-flight stream abort হয়। ঘণ্টাব্যাপী চলা ডাউনলোডের জন্য ৫ সেকেন্ড
   অর্থহীন। উপরন্তু MultiPartDownloader কনস্ট্রাক্টরে _httpClient একবার
   capture করে রাখে, তাই সেই ডাউনলোডের নতুন segment request-ও disposed
   client ব্যবহার করবে → ডাউনলোড স্থায়ীভাবে অচল।
   → পুরনো handler কখনো জোর করে dispose করবে না যতক্ষণ তার উপর in-flight
     request আছে; reference counting বা "drain then dispose" প্যাটার্ন
     ব্যবহার করো। এবং MultiPartDownloader যেন client capture না করে —
     প্রতিটি request-এর সময় provider থেকে current client নেবে।

৬. MEDIUM — _graceTimer লকের ভেতরে dispose/null হয় কিন্তু লকের বাইরে
   assign হয়। ৫ সেকেন্ডের মধ্যে দুটি rebuild হলে প্রথম পুরনো client
   কখনোই dispose হয় না → handler ও তার সব socket লিক।
   → সম্পূর্ণ timer lifecycle লকের ভেতরে নাও।

৭. MEDIUM — InitialHttp2StreamWindowSize = 16 MB × সর্বোচ্চ ৬৪ stream ≈
   তাত্ত্বিকভাবে ১ GB receive-window buffer commitment।
   → ১-২ MB-তে নামাও, configurable রাখো।

৮. MEDIUM — ডিফল্ট User-Agent "EDM/1.0 (+https://example)" — একটি
   placeholder URL প্রোডাকশনে শিপ করা হয়েছে। Cloudflare/Akamai এমন UA-কে
   বট হিসেবে চিহ্নিত করে ব্লক বা challenge করে। IDM ডিফল্টে browser-সদৃশ
   UA ব্যবহার করে, তাই এটি বাস্তব দুনিয়ার সাফল্যের হারে সরাসরি পার্থক্য।
   → placeholder URL সরাও, প্রকৃত প্রোডাক্ট URL দাও, এবং একটি configurable
     browser-সদৃশ UA option যোগ করো (per-download override সহ)।

৯. MEDIUM — শেয়ার্ড SocketsHttpHandler-এ ConnectTimeout সেট করা নেই
   (ডিফল্ট infinite), তাই একটি blackholed host একজন ওয়ার্কারকে ১০ মিনিট
   আটকে রাখবে। মজার ব্যাপার — অব্যবহৃত HttpClientProvider ক্লাসটি ৩০ সেকেন্ড
   সঠিকভাবে সেট করে।
   → ConnectTimeout ৩০ সেকেন্ড সেট করো।

১০. আর্কিটেকচার — HttpClientProvider.cs একটি দ্বিতীয়, প্রতিদ্বন্দ্বী HTTP
    abstraction (ডিফল্ট MaxConnectionsPerServer = 100, ৩০ সেকেন্ড
    ConnectTimeout, কোনো proxy/decompression/HTTP-2/UA wiring নেই, এবং
    UpdateSettings-এ কোনো grace ছাড়াই সাথে সাথে dispose করে)। এটি সম্ভবত
    orphan।
    → দুটোর মধ্যে একটি রাখো (SharedHttpClient-কে base ধরো), HttpClientProvider-
      এর ভালো দিকগুলো (ConnectTimeout) সেখানে নাও, তারপর অন্যটি মুছে দাও।
      সব কলার একটি abstraction ব্যবহার করবে।
    → একই ভাবে: ব্যান্ডউইথ থ্রটলিংয়ের ৪টি প্রতিদ্বন্দ্বী ইমপ্লিমেন্টেশন আছে —
      BandwidthThrottler, TokenBucketBandwidthLimiter, UnifiedBandwidthGovernor,
      এবং DownloadSingleAsync-এর inline লজিক। একটি token-bucket gover­nor-এ
      একত্র করো যা global এবং per-download — দুই ধরনের লিমিট সমর্থন করে।

১১. HIGH — per-download স্পিড লিমিট সম্পূর্ণ অকার্যকর:
    SegmentWorker.cs লাইন 38-এর speedLimitProvider প্যারামিটার গ্রহণ করা হয়
    কিন্তু মেথড বডিতে কখনো ব্যবহৃত হয় না (লাইন 37-এর progressReporter-ও
    একইভাবে অব্যবহৃত)। MultiPartDownloader যথাযথভাবে ThrottleKbps পাস করে,
    কিন্তু তা নীরবে উপেক্ষিত হয় — শুধু global BandwidthThrottler কাজ করে
    (লাইন 190)। অর্থাৎ UI-তে per-download স্পিড লিমিট অপশন আছে কিন্তু
    কার্যকারিতা নেই, যেখানে IDM-এ এটি কাজ করে।
    → unified throttler-এ per-download লিমিট wire করো এবং প্রমাণ করো একটি
      নির্দিষ্ট KB/s লিমিট দিলে প্রকৃত throughput সেই সীমার মধ্যে থাকে।
      অব্যবহৃত progressReporter প্যারামিটারটি হয় ব্যবহার করো, নাহয় সরাও।

শেষে dotnet build + dotnet test। যাচাই করো: (ক) HEAD ব্লক করা সার্ভার থেকে
ডাউনলোড কাজ করে, (খ) per-download লিমিট প্রকৃতপক্ষে প্রয়োগ হয়, (গ) ধীর
সংযোগে ১০ মিনিটের বেশি সময় নেওয়া সেগমেন্ট সফল হয়। রিপোর্ট দাও।
```

---

# ফেজ ৫ — ভার্সন

## P16 · ভার্সন সমন্বয় ও ভুয়া আর্টিফ্যাক্ট

```text
EDM ইকোসিস্টেমজুড়ে ভার্সন স্ট্রিং অসামঞ্জস্যপূর্ণ, এবং একটি ভুয়া রিলিজ
আর্টিফ্যাক্ট সক্রিয়ভাবে বিতরণ হচ্ছে। এটি CRITICAL।

প্রামাণিক ভার্সন: 1.0.0.0 — EDM/EDM.csproj লাইন 18-21 (Version,
AssemblyVersion, FileVersion, ProductVersion — চারটিই)। এটিই কম্পাইল করা
বাইনারিতে এমবেড হয়, তাই এটিই একমাত্র সত্য।

আসল আর্টিফ্যাক্ট: EDM-Setup-v1.0.0.exe — 34,264,221 বাইট,
SHA-256 প্রিফিক্স e94ca3ea…  (পূর্ণ হ্যাশ release-manifest.json বা রুট
update.json-এ আছে)

ভুয়া আর্টিফ্যাক্ট: website/downloads/EDM-Setup-v2.1.0.exe এবং
website/downloads/EDM-Setup-v2.0.0.exe সম্পূর্ণ একই ফাইল — দুটোরই সাইজ
19,807,971 বাইট, SHA-256 প্রিফিক্স 93049cf8…। অর্থাৎ "v2.1.0" নামে কোনো
স্বতন্ত্র বিল্ড কখনো হয়নি; v2.0.0 ফাইলটাই নতুন নামে কপি করা হয়েছে। এই
আর্টিফ্যাক্টটি release-manifest.json-এ নেই, তাই কোনো verified hash রেকর্ড
বা নিয়ন্ত্রিত রিলিজ প্রক্রিয়াও নেই।

যা করতে হবে:

১. website/downloads/ থেকে EDM-Setup-v2.0.0.exe ও EDM-Setup-v2.1.0.exe
   সম্পূর্ণ সরাও।

২. ব্যাকএন্ড সিডিং — EDM.ControlPlane.Api/Program.cs লাইন 338-358:
   Version "2.1.0" → "1.0.0"
   Title "EDM 2.1.0 Turbo Release" → ভার্সন-নিরপেক্ষ বা "1.0.0"
   ArtifactName "EDM-Setup-v2.1.0.exe" → "EDM-Setup-v1.0.0.exe"
   Sha256Hash "93049cf8…" → আসল e94ca3ea… পূর্ণ হ্যাশ
   FileSizeBytes 19807971 → 34264221
   (এটি সবচেয়ে বিপজ্জনক অসামঞ্জস্য — কন্ট্রোল প্লেন এই ভুয়া আর্টিফ্যাক্টটিকেই
   প্রামাণিক হিসেবে নিবন্ধিত করেছে, তাই auto-update চ্যানেল ব্যবহারকারীদের
   v1.0.0 থেকে একটি পুরনো ভিন্ন বিল্ডে "আপগ্রেড" করানোর পথে আছে।)

৩. EDM.ControlPlane.Api/Services/AuthService.cs লাইন ≈492-এর hardcoded
   AppVersion = "2.0.0" সরাও — ক্লায়েন্ট-রিপোর্টেড ভার্সন ব্যবহার করো।

৪. ড্যাশবোর্ড — নিচের প্রতিটি জায়গায় v2.1.0/v2.0.0 আছে:
   api.js লাইন 116 ও 223; app.js ≈327; devices.js ≈23;
   index.html লাইন 119, 469, 825, 834, 1084, 1098, 1139, 1483, 1633, 1650;
   mock-data.js লাইন 18, 40, 41, 43, 85, 90 (v2.1.0 ও
   "EDM-Setup-2.1.0-x64.exe")
   index.html লাইন 1650-এ সাইজও ভুল ("2.4 MB", প্রকৃত ৩৪ MB)।
   → সব hardcoded ভার্সন সরাও, একটি build-time-injected constant ব্যবহার করো।

৫. পাবলিক ওয়েবসাইট — website/index.html:
   লাইন 273 "Build v2.1.0 • 19.8 MB" → v1.0.0 • 34.3 MB
   লাইন 280 "Download EDM Setup.exe (19.8 MB)" → 34.3 MB
   লাইন 282 href="/downloads/EDM-Setup-v2.1.0.exe" → EDM-Setup-v1.0.0.exe
   লাইন 376 badge "v2.1.0 (Latest)" → v1.0.0 (Latest)

৬. edm-wordpress-theme/front-page.php লাইন 26-এর JSON-LD
   "softwareVersion": "2.1.0" → "1.0.0"। (এটি সার্চ ইঞ্জিনে structured data
   হিসেবে ভুল ভার্সন index করাচ্ছে।)

৭. EDM/update.json — version "1.0.0" ঠিক আছে, কিন্তু sha256 খালি ("") এবং
   URL "https://example.com/edm/EDM-1.0.0.zip" একটি placeholder। এই ফাইলটি
   EDM.csproj-এ CopyToOutputDirectory="PreserveNewest" দিয়ে বিল্ড আউটপুটে
   কপি হয়, অর্থাৎ শিপ করা অ্যাপটি এই ভাঙা ম্যানিফেস্টটিই বহন করে —
   আপডেট integrity verification কার্যত অক্ষম।
   → sha256 পূরণ করো, প্রকৃত URL দাও, অথবা রুট update.json-এর সাথে একীভূত
     করো যাতে দুটি প্রতিদ্বন্দ্বী ম্যানিফেস্ট না থাকে।

৮. release-manifest.json-এর "secretsScanClean" দাবি সংশোধন করো (P1-এর কাজ
   শেষ হওয়ার পর true করা যাবে, তার আগে নয়), এবং authenticodeSigned: false
   একটি TODO হিসেবে ডকুমেন্ট করো।

৯. একটি single source of truth প্রতিষ্ঠা করো: EDM/EDM.csproj-এর Version
   থেকে বিল্ড টাইমে API, ড্যাশবোর্ড, ওয়েবসাইট ও extension manifest-এ ভার্সন
   inject করো। হাতে লেখা ভার্সন স্ট্রিং যেন আর কোথাও না থাকে।

১০. CI-তে একটি version-consistency gate যোগ করো যা কোনো মিসম্যাচ পেলে
    বিল্ড ফেল করবে — csproj vs update.json vs release-manifest.json vs
    extension manifest vs ওয়েবসাইট, এবং আর্টিফ্যাক্টের actual hash/size
    বনাম ম্যানিফেস্টে ঘোষিত মান।

শেষে একটি grep চালাও যা প্রমাণ করে কোথাও 2.0.0 বা 2.1.0 অবশিষ্ট নেই
(CHANGELOG-এর ঐতিহাসিক এন্ট্রি বাদে), এবং প্রতিটি পরিবর্তিত ফাইল+লাইন সহ
রিপোর্ট দাও।
```

---

# ফেজ ৬ — ওয়েব

## P17 · ওয়েবসাইট SEO ও পারফরম্যান্স

```text
website/ ফোল্ডারের EDM অফিসিয়াল ওয়েবসাইটে SEO ও পারফরম্যান্স সমস্যা ঠিক করো।

১. HIGH — website/sitemap.xml-এ ১৪টি URL তালিকাভুক্ত (index.html ছাড়া
   features.html, technology.html, browser-extension.html, download.html,
   pricing.html, screenshots.html, changelog.html, faq.html,
   system-requirements.html, privacy.html, terms.html, support.html,
   about.html), কিন্তু website/ ফোল্ডারে কেবল index.html বিদ্যমান।
   অর্থাৎ ১৩টি sitemap URL-ই dead। আরও খারাপ: ControlPlane API-র
   Program.cs (≈লাইন 450-475) MapFallback অজানা path-এ index.html সার্ভ করে,
   তাই crawler ৪০৪ না পেয়ে ২০০-OK duplicate content পায় — একটি গুরুতর
   SEO/duplicate-content সমস্যা।
   → সিদ্ধান্ত নাও: হয় ১৩টি পেজ প্রকৃতপক্ষে তৈরি করো, নাহয় sitemap থেকে
     সরিয়ে শুধু বিদ্যমান URL রাখো। কোনো মধ্যপথ নেই।
   → Program.cs-এর MapFallback শুধু জ্ঞাত SPA route-এ সীমিত করো; অন্যান্য
     অজানা path-এ প্রকৃত ৪০৪ দাও, এবং /api/ প্রিফিক্সে JSON ৪০৪ দাও।

২. HIGH — website/index.html-এ canonical URL, og:image, robots meta এবং
   theme-color — একটিও নেই (grep-এ ০ ম্যাচ)। og:image না থাকায় twitter:card
   = summary_large_image রেন্ডার করতে পারে না, অর্থাৎ সোশ্যাল শেয়ারে কোনো
   preview কার্ড আসে না — কনভার্শনে সরাসরি ক্ষতি।
   → canonical, og:image (একটি প্রকৃত 1200×630 ইমেজ তৈরি করে), og:site_name,
     robots, theme-color যোগ করো। twitter:image-ও দাও।

৩. HIGH — <head>-এ https://unpkg.com/lucide@latest একটি blocking,
   unversioned, SRI-হীন স্ক্রিপ্ট। এটি render-blocking, একটি সাপ্লাই-চেইন
   ঝুঁকি, এবং @latest মানে যেকোনো দিন breaking change।
   → নির্দিষ্ট ভার্সনে pin করো, self-host করো (সবচেয়ে ভালো), অথবা
     integrity + crossorigin অ্যাট্রিবিউট যোগ করো। defer করো।

৪. MEDIUM — assets/css/landing.css ৪৫ KB ও dashboard.css ৩৯ KB — সবই
   unminified এবং render path-এ। মোট assets/css ১৭৭ KB, assets/js ১৫২ KB।
   → CSS/JS minify ও bundle করো, critical CSS inline করো, বাকিটা defer/
     async লোড করো। একটি সাধারণ বিল্ড স্টেপ যোগ করো।

৫. MEDIUM — sitemap.xml-এর সব lastmod = 2025-06-18, ১৪ মাসের পুরনো।
   → আপডেট করো এবং বিল্ড টাইমে জেনারেট করার ব্যবস্থা করো।

৬. MEDIUM — robots.txt-এ Disallow: /admin/ আছে, কিন্তু প্রকৃত অ্যাডমিন path
   /edm-admin, তাই intended block কাজ করছে না। উপরন্তু index.html-এর nav-এ
   /edm-admin-এ target="_blank" লিংক আছে — অ্যাডমিন কনসোল পাবলিকলি
   বিজ্ঞাপিত হচ্ছে।
   → robots.txt-এ /edm-admin যোগ করো এবং পাবলিক nav থেকে অ্যাডমিন লিংক সরাও।

৭. MEDIUM — responsive.css মাত্র ৫ KB, অথচ landing.css ৪৫ KB — মোবাইল
   কভারেজ সম্ভবত অপর্যাপ্ত।
   → ৩৭৫px, ৭৬৮px, ১০২৪px, ১৪৪০px-এ প্রতিটি সেকশন যাচাই করো এবং যা ভাঙছে
     তা ঠিক করো। মোবাইল nav ও গ্রিড breakpoint বিশেষভাবে দেখো।

৮. MEDIUM — আইকন <i data-lucide> দিয়ে inject হয়, কোনো aria-label বা
   aria-hidden নেই; JS ফেইল করলে nav-এ শুধু খালি এলিমেন্ট থাকবে।
   → decorative আইকনে aria-hidden="true", meaningful আইকনে aria-label দাও।
     সব ইন্টারঅ্যাক্টিভ এলিমেন্টে accessible name নিশ্চিত করো।

৯. INFO — website/ ও edm-wordpress-theme/ দুটি ডুপ্লিকেট WordPress থিম ট্রি,
   এবং website/backup_css/ একটি dead ফোল্ডার। এছাড়া পোর্টফোলিও CSS দুই
   জায়গায় ডুপ্লিকেট (website/my portfolio/ ও website/assets/css/portfolio/)।
   → কোনটি canonical তা নির্ধারণ করো, বাকিগুলো মুছে দাও। মুছার আগে diff
     নিয়ে রিপোর্টে দেখাও।

শেষে Lighthouse চালাও (mobile ও desktop) এবং before/after স্কোর সহ
রিপোর্ট দাও।
```

## P18 · পোর্টফোলিও ব্যাকএন্ড হার্ডেনিং

```text
website/my portfolio/server.js (৩৪০ লাইন) একটি Node/Express + socket.io +
Telegram bot + Google Gemini ব্যাকএন্ড। এতে একটি CRITICAL ও কয়েকটি HIGH
সমস্যা আছে।

গুরুত্বপূর্ণ: P1-এ .env সিক্রেট রোটেশন ইতোমধ্যেই হয়ে থাকতে হবে। না হলে
আগে সেটি করুন।

১. CRITICAL — লাইন ≈235-265-এর POST /api/payment/initiate রেসপন্স বডিতে
   একটি headers অবজেক্ট রিটার্ন করে যার মধ্যে
   Authorization: Bearer <SMAT_GLOBAL_TOKEN> সরাসরি বসানো থাকে। এন্ডপয়েন্টে
   কোনো authentication নেই, কোনো rate limit নেই, এবং লাইন 13-এ
   app.use(cors()) — ওয়াইল্ডকার্ড CORS, তাই যেকোনো ওয়েবসাইট থেকে ব্রাউজারেই
   টোকেন তোলা যায়। এটি .env কমিট করার চেয়েও খারাপ, কারণ git হিস্টরি
   পরিষ্কার করেও এটি বন্ধ হবে না — সার্ভার রানটাইমে সক্রিয়ভাবে সিক্রেট
   বিতরণ করছে।
   → রেসপন্স থেকে headers/Authorization সম্পূর্ণ সরাও। পেমেন্ট গেটওয়ে কল
     সম্পূর্ণভাবে সার্ভার-সাইডে করো; টোকেন কখনো ক্লায়েন্টে যাবে না।
     ক্লায়েন্ট শুধু একটি redirect URL বা payment session id পাবে।

২. HIGH — লাইন 13 app.use(cors()) এবং লাইন 22-27 socket.io
   origin: '*' — ওয়াইল্ডকার্ড।
   → একটি explicit allow-list দাও (আপনার প্রকৃত ডোমেইন), credentials
     হ্যান্ডলিং সঠিকভাবে কনফিগার করো।

৩. HIGH — /api/chatbot ও /api/payment/initiate-এ কোনো rate limit নেই, এবং
   /api/chatbot Telegram alert ট্রিগার করে → spam amplification vector।
   → express-rate-limit যোগ করো, per-IP এবং per-session লিমিট সহ।
     Telegram notification-এ একটি আলাদা, কঠোর throttle দাও।

৪. HIGH — লাইন ≈53-54-এর liveChatSessions ও conversationHistory কখনো
   evict হয় না, এবং sessionId কলার-প্রদত্ত (লাইন ≈207)। আক্রমণকারী unique
   sessionId পাঠিয়ে অসীম মেমরি বৃদ্ধি ঘটাতে পারে।
   → server-generated session id ব্যবহার করো, TTL + LRU eviction যোগ করো,
     এবং সর্বোচ্চ session সংখ্যা ও per-session history দৈর্ঘ্য সীমিত করো।

৫. MEDIUM — লাইন ≈141-এ bot.sendMessage(MY_CHAT_ID, text,
   { parse_mode: 'Markdown' }) — text-এ unsanitized ইউজার ইনপুট আছে।
   Malformed markdown-এ API ব্যর্থ হবে, এবং content spoofing সম্ভব।
   → markdown special character escape করো, অথবা parse_mode সম্পূর্ণ বাদ
     দিয়ে plain text পাঠাও। দৈর্ঘ্য সীমিত করো।

৬. MEDIUM — লাইন ≈164-179-এ প্রতিটি Gemini request-এ সম্পূর্ণ knowledgeData
   JSON inline করা হয়, সাথে raw ইউজার query জোড়া দেওয়া হয় — প্রতি request-এ
   বড় token অপচয় এবং prompt injection ঝুঁকি।
   → knowledge base ছোট করো বা একটি retrieval স্টেপ যোগ করো (শুধু প্রাসঙ্গিক
     অংশ পাঠাও)। ইউজার ইনপুট স্পষ্ট delimiter-এ আবদ্ধ করো এবং system
     instruction-এ বলো delimiter-এর ভেতরের টেক্সট কেবল ডেটা, নির্দেশ নয়।
     সর্বোচ্চ ইনপুট দৈর্ঘ্য enforce করো।

৭. সার্বিক — সব environment variable স্টার্টআপে validate করো; কোনোটি
   অনুপস্থিত থাকলে স্পষ্ট error দিয়ে exit করো (নীরবে খালি স্ট্রিং নিয়ে
   চলবে না, যেমন এখন GEMINI_API_KEY খালি অবস্থায় হচ্ছে)।

৮. Express-এ helmet যোগ করো, error handler থেকে stack trace সরাও,
   এবং JSON body size limit দাও।

শেষে সার্ভার চালিয়ে প্রতিটি এন্ডপয়েন্ট ম্যানুয়ালি যাচাই করো — বিশেষভাবে
প্রমাণ করো /api/payment/initiate-এর রেসপন্সে কোনো টোকেন নেই। রিপোর্ট দাও।
```

---

# ফেজ ৭ — হাইজিন ও টেস্ট

## P19 · API হার্ডেনিং (অবশিষ্ট Medium/Low)

```text
EDM.ControlPlane.Api-তে অবশিষ্ট Medium ও Low severity সমস্যাগুলো ঠিক করো।
এগুলো স্বতন্ত্র, একটির উপর অন্যটি নির্ভরশীল নয়।

১. MEDIUM — কোনো EF Core migration নেই; Program.cs (≈লাইন 194)
   db.Database.EnsureCreated() ব্যবহার করে। ফলে schema evolution-এর কোনো
   পথ নেই — প্রথম মডেল পরিবর্তনেই প্রোডাকশন ডেটাবেস ম্যানুয়াল হস্তক্ষেপ
   দাবি করবে।
   → একটি initial migration তৈরি করো, EnsureCreated() সরিয়ে
     Migrate() ব্যবহার করো (বা স্টার্টআপে migration না চালিয়ে একটি আলাদা
     deploy step রাখো, যা বহু-ইনস্ট্যান্সে নিরাপদ)।

২. MEDIUM — Middleware/GlobalExceptionHandlingMiddleware.cs ৪xx রেসপন্সে
   exception.Message হুবহু ফেরত দেয়। EF/InvalidOperationException-এর
   মেসেজে মডেল ও SQL internal ফাঁস হতে পারে। এছাড়া রেসপন্স শুরু হওয়ার পর
   exception ঘটলে (যেমন বড় ফাইল streaming-এর মাঝে) context.Response.StatusCode
   সেট করা নিজেই throw করবে এবং মূল error ঢাকা পড়বে।
   → 4xx-এও নিয়ন্ত্রিত, নিরাপদ মেসেজ দাও (একটি error code + generic text);
     আসল মেসেজ শুধু লগে যাবে। HasStarted গার্ড যোগ করো।

৩. MEDIUM — Middleware/SecurityHeadersMiddleware.cs:
   - Strict-Transport-Security শর্তহীনভাবে পাঠানো হয়, HTTP-র উপরেও
   - obsolete X-XSS-Protection হেডার আছে
   - API রেসপন্সে Cache-Control: no-store নেই
   → HSTS কেবল HTTPS রিকোয়েস্টে দাও এবং preload বিবেচনা করো।
     X-XSS-Protection সরাও। /api/ রেসপন্সে no-store যোগ করো।
   → Program.cs-এ UseHttpsRedirection() ও UseHsts() যোগ করো (বর্তমানে
     দুটোই অনুপস্থিত)।
   → appsettings.json-এর "AllowedHosts": "*" প্রকৃত হোস্ট তালিকায় বদলাও।

৪. MEDIUM — Middleware/CsrfProtectionMiddleware.cs যেকোনো
   "Authorization: Bearer " প্রিফিক্স দেখলেই CSRF যাচাই এড়িয়ে যায়, টোকেন
   বৈধ কি না দেখে না। খালি "Bearer " পাঠালে JwtBearer হ্যান্ডলার কুকি-
   fallback-এ চলে যায় → কুকি দিয়ে auth, CSRF skip।
   এছাড়া bypass তালিকায় exact path.Equals ম্যাচিং ব্যবহৃত, তাই trailing
   slash / case / alternate route ভ্যারিয়েন্টে আচরণ অসামঞ্জস্যপূর্ণ।
   → Bearer bypass সরাও, অথবা কেবল প্রকৃত bearer-authenticated রিকোয়েস্টে
     (কুকি ব্যবহার না করে) প্রযোজ্য করো। path ম্যাচিং normalize করো
     (case-insensitive, trailing slash trim)।
   → POST /api/v1/support/tickets-এর CSRF ও auth exemption পুনর্বিবেচনা করো —
     এটি P7-এর XSS চেইনের ইনজেকশন পয়েন্ট ছিল। অন্তত CAPTCHA বা কঠোর rate
     limit দাও।

৫. MEDIUM — Services/Argon2idPasswordHasher.cs-এর VerifyPassword (≈লাইন 62)
   encoded hash split করে parts[1] != "argon2id" যাচাই করে, কিন্তু embedded
   m=/t=/p= প্যারামিটার সম্পূর্ণ উপেক্ষা করে এবং সবসময় compile-time
   constant ব্যবহার করে। ফলে কোনো algorithm agility নেই, এবং ভবিষ্যতে
   প্যারামিটার বাড়ালে সব ইউজার একসাথে lock-out হবে।
   → সংরক্ষিত hash থেকে প্যারামিটার পড়ে যাচাই করো। rehash-on-verify
     upgrade path যোগ করো (পুরনো প্যারামিটারে সফল verify হলে নতুন
     প্যারামিটারে re-hash করে সংরক্ষণ করো)।

৬. MEDIUM — পাসওয়ার্ডে কোনো maximum length নেই, এবং Argon2id প্রতি হ্যাশে
   ৬৪ MB × ৪ থ্রেড খরচ করে — এটি একটি DoS ভেক্টর। পাসওয়ার্ড পলিসিও কেবল
   length >= 8, কোনো complexity নিয়ম নেই।
   → maximum length (যেমন ১২৮) enforce করো, এবং একটি যুক্তিসঙ্গত
     complexity/breach-list চেক যোগ করো।

৭. MEDIUM — AuthService.cs-এ detailsJson কয়েক জায়গায় raw string
   interpolation দিয়ে তৈরি হয় (যেমন ≈লাইন 421), যেখানে email ও
   googleSubject অ্যাটাকার-নিয়ন্ত্রিত — অর্থাৎ অডিট লগে JSON injection,
   যা forensic নির্ভরযোগ্যতা নষ্ট করে।
   → সব জায়গায় JsonSerializer ব্যবহার করো।

৮. HIGH — AuthService.cs (≈লাইন 240-248) _failedLoginTrackers অস্তিত্বহীন
   ইউজারের জন্যও এন্ট্রি তৈরি করে এবং কখনো evict করে না (মেমরি লিক)।
   লকআউট কেবল identifier-ভিত্তিক, তাই যেকোনো অ্যাডমিনকে ইচ্ছেমতো ৫টি
   ব্যর্থ চেষ্টা দিয়ে ১৫ মিনিট লক করে রাখা যায় (lockout DoS)।
   → TTL/LRU eviction যোগ করো। লকআউট শুধু identifier নয়, IP+identifier
     সমন্বয়ে করো, এবং progressive delay ব্যবহার করো যাতে একজন আক্রমণকারী
     বৈধ ইউজারকে লক করে দিতে না পারে।

৯. MEDIUM — AuthService.cs (≈লাইন 570-700) process-wide _refreshLock
   সম্পূর্ণ refresh flow (DB round-trip সহ) serialize করে — বহু-ইনস্ট্যান্স
   ডিপ্লয়মেন্টে ভুল, single instance-এও throughput ঘাতক।
   → লকের scope per-user বা per-token-family-তে নামাও, অথবা DB-লেভেল
     optimistic concurrency ব্যবহার করো।

১০. MEDIUM — LogoutAsync সেশন রো revoke করে কিন্তু ইস্যু করা access token
    ~১৫ মিনিট বৈধ থাকে, কারণ validation-এ কোনো denylist চেক নেই।
    → টোকেনে jti claim যোগ করো এবং একটি short-TTL denylist (memory cache
      বা Redis) চেক করো, যাতে logout তাৎক্ষণিক হয়।

১১. LOW — Services/TokenService.cs-এর HashToken salt/HMAC ছাড়া plain
    SHA-256। ২৫৬-বিট refresh token-এ গ্রহণযোগ্য, কিন্তু কম-entropy
    recovery code-এ দুর্বল।
    → recovery code-এর জন্য একটি keyed HMAC বা password hasher ব্যবহার করো।

১২. INFO — Program.cs (≈লাইন 197) স্টার্টআপে .GetAwaiter().GetResult()
    sync-over-async। → async স্টার্টআপ প্যাটার্নে সরাও।

১৩. INFO — csproj-এ JwtBearer/EFCore/OpenApi/Npgsql সব 9.0.0 কিন্তু target
    net10.0। → matching 10.x প্যাকেজে আপগ্রেড করো এবং build warning যাচাই করো।

প্রতিটি আইটেম আলাদা কমিটে করো। শেষে dotnet build + dotnet test এবং রিপোর্ট দাও।
```

## P20 · এক্সেপশন হ্যান্ডলিং ও ডুপ্লিকেট কোড

```text
EDM কোডবেসে দুটি ব্যাপক code-quality সমস্যা আছে। এগুলো কোনো ফিচার পরিবর্তন
করবে না, কিন্তু ভবিষ্যতের সব ডিবাগিং সহজ করবে।

১. শতাধিক খালি catch { } ব্লক — এটি সবচেয়ে ব্যাপক সমস্যা। শুধু
   EDM/Services/MultiPartDownloader.cs-এ ১০+ (≈লাইন 177-180, 185, 264-267,
   445, 484, 485, 548-551, 559, 612, 628)। এর মধ্যে সম্পূর্ণ adaptive loop
   এবং সম্পূর্ণ telemetry loop-ও আছে — অর্থাৎ সেগুলো throw করলে ফিচারটি
   নীরবে মৃত হয়ে যায় এবং কেউ কখনো জানে না।
   অন্যান্য নিশ্চিত জায়গা:
   - EDM/Services/DurableMetadataManager.cs লাইন 113 (.bak কপি ব্যর্থতা —
     ফলে backup নীরবে বহু generation পুরনো থাকতে পারে), 123, 168, 198,
     308, 326, 385-388, 407, 410
   - EDM/Services/SegmentWorker.cs লাইন 193 (BandwidthThrottler ব্যর্থতা)
   → পুরো EDM/ ও EDM.ControlPlane.Api/ স্ক্যান করো। প্রতিটি খালি catch-এ:
     হয় নির্দিষ্ট exception type ধরো এবং কারণ সহ কমেন্ট লেখো, নাহয়
     LoggingService দিয়ে অন্তত warning/debug লগ করো। OperationCanceledException
     আলাদা করে হ্যান্ডল করো যাতে cancellation আর error আলাদা থাকে।
     কোনো catch যদি সত্যিই উপেক্ষা করার যোগ্য হয়, সেখানে কেন তা এক লাইনে
     লেখো — নীরব catch রাখবে না।
   → একটি analyzer rule বা CI grep যোগ করো যা নতুন খালি catch ধরলে ফেল করবে।

২. ডুপ্লিকেট ও orphan অ্যাবস্ট্রাকশন:
   - EDM/Services/HttpClientProvider.cs — SharedHttpClient-এর প্রতিদ্বন্দ্বী,
     সম্ভবত orphan (P15-এ একীভূত হওয়ার কথা; না হলে এখন করো)
   - ৪টি ব্যান্ডউইথ থ্রটল ইমপ্লিমেন্টেশন: BandwidthThrottler,
     TokenBucketBandwidthLimiter, UnifiedBandwidthGovernor, এবং
     MultiPartDownloader-এর inline লজিক (P15/P14-এ একীভূত হওয়ার কথা)
   - EDM/Services/PauseTokenSource.cs — সঠিক অ্যাবস্ট্রাকশন, কিন্তু
     অব্যবহৃত (P11-এ wire হওয়ার কথা)
   - EDM/Services/PerDiskTempStorageManager.cs — লেখা হয়েছে কিন্তু wire
     করা হয়নি; ফলে temp ডিরেক্টরি destination ফোল্ডারেই তৈরি হয়, অর্থাৎ
     OneDrive/Dropbox সিঙ্ক ফোল্ডারে ডাউনলোড করলে প্রতিটি .part ফাইল
     ক্লাউডে আপলোড হয়। → এটি wire করো, অথবা সিঙ্ক-ফোল্ডার ডিটেক্ট করে
     temp অন্যত্র রাখো।
   - extension/ , Output/extension/ , tools/*-extension/ — এক্সটেনশন সোর্সের
     ৩টি কপি। → একটি canonical সোর্স রাখো এবং বাকিগুলো একটি বিল্ড স্টেপ
     থেকে জেনারেট করো। মুছার আগে তিনটির diff রিপোর্টে দেখাও।
   - SegmentWorker.cs লাইন 37-এর progressReporter প্যারামিটার অব্যবহৃত।
     → ব্যবহার করো নাহয় সরাও।
   → এছাড়া `Task.Run(..., token)` প্যাটার্নের ভুল ব্যবহার খোঁজো — token
     শুধু task *শুরু* হওয়া আটকায়, চলমান body ক্যানসেল করে না। যেখানে
     ভুল বোঝাবুঝি আছে সেখানে token সরাসরি body-তে পাস করো।
   → EDM.csproj-এ `<Compile Remove="EDM_new\**\*.cs" />` আছে — একটি সমান্তরাল
     dev snapshot tree রিপোতে রাখা কিন্তু বিল্ড থেকে বাদ। এটি এখনো দরকার কি
     না নির্ধারণ করো; না হলে মুছে দাও।

৩. গিট হাইজিন — রিপোতে ~১০৮ MB installer ও ২৯টি DLL/PDB/EXE ট্র্যাকড।
   → বাইনারি গিট থেকে সরিয়ে GitHub Release asset বা Git LFS-এ নাও।

প্রতিটি অংশ আলাদা কমিটে করো। কোনো behavior পরিবর্তন হচ্ছে না তা নিশ্চিত
করতে dotnet test চালাও এবং রিপোর্ট দাও।
```

## P21 · রিগ্রেশন টেস্ট ও ডকুমেন্টেশন সংশোধন

```text
শেষ ধাপ। EDM-এর টেস্ট স্যুট ও certification ডকুমেন্টেশন বিশ্বাসযোগ্য করো।

পটভূমি: রিপোতে ~২৭,৯০০ LOC / ~২০০ টেস্ট ফাইল আছে (xUnit + FluentAssertions),
কিন্তু এই স্যুট নিচের মৌলিক বাগগুলোর একটিও ধরতে পারেনি:
- Pause/Resume lost-wakeup deadlock
- RetryCount ক্লোনে হারিয়ে অসীম retry loop
- work-stealing-এর কারণে সেগমেন্ট অকালে Completed হয়ে ফাইল truncation
- resume-এ incremental SHA-256 ভুল হওয়া
- rate limit policy কোনো এন্ডপয়েন্টে অ্যাটাচ না থাকা
- WebAuthn signature কখনো যাচাই না হওয়া
- Google ID token signature কখনো যাচাই না হওয়া

অর্থাৎ স্যুটটি happy path ভালো কভার করে কিন্তু race condition, fault
injection এবং negative security case কভার করে না।

১. প্রতিটি ঠিক করা বাগের জন্য একটি regression test লেখো যা ফিক্সের আগে
   ফেল করত। বিশেষভাবে:
   - Pause/Resume: ডাউনলোড চলাকালীন এলোমেলো বিরতিতে (১-২০ ms) ৫০+ বার টগল,
     প্রমাণ করো hang হয় না
   - Retry: একটি সেগমেন্ট ধারাবাহিকভাবে ব্যর্থ করানো mock দিয়ে প্রমাণ করো
     নির্দিষ্ট retry-র পর ডাউনলোড স্পষ্ট error সহ শেষ হয়
   - Work stealing: প্রমাণ করো steal-এর পর কোনো সেগমেন্ট short file নিয়ে
     Completed হয় না এবং চূড়ান্ত ফাইল byte-for-byte সঠিক
   - Resume hash: আংশিক ডাউনলোড → resume → প্রমাণ করো সম্পূর্ণ সেগমেন্টের
     hash সঠিক
   - Merge/assembly: একটি অংশ মুছে দিয়ে প্রমাণ করো স্পষ্ট integrity error
     আসে (FileNotFoundException নয়)
   - Rate limit: প্রমাণ করো লিমিট ছাড়ালে 429 আসে
   - Passkey ও Google login: invalid signature-সহ টোকেন/assertion দিয়ে
     প্রমাণ করো ৪০১ আসে
   - XSS: টিকিট বডিতে HTML পেলোড দিয়ে প্রমাণ করো তা escape হয়ে রেন্ডার হয়

২. Fault injection যোগ করো: নেটওয়ার্ক ড্রপ, ধীর সার্ভার, HEAD ৪০৫,
   ৪০৪ মাঝপথে, disk full, প্রক্রিয়া kill (crash-resume) — এই পরিস্থিতিগুলোর
   জন্য টেস্ট।

৩. একটি throughput benchmark test যোগ করো যা একটি লোকাল test server থেকে
   ডাউনলোড করে MB/s রিপোর্ট করে, যাতে ভবিষ্যতে performance regression ধরা
   পড়ে। P12/P13/P14-এর আগে-পরের সংখ্যা এতে রেকর্ড করো।

৪. docs/KNOWN_ISSUES.md সংশোধন করো। বর্তমানে এতে দাবি করা আছে:
   - "Memory Delta (Pre/Post) +4.12 MB … PASS (Zero Leaks)"
   - "Pause/Resume Toggles 10 Cycles @ 100ms … PASS (No Deadlock)"
   - AdaptiveConnectionManager "automatically steps down per metered network type"
   এই তিনটি দাবিই কোড-লেভেলে মিথ্যা প্রমাণিত হয়েছিল (deadlock race,
   unbounded workerTasks/_workerTelemetry/lastSegSamples/chunkStatsMap, এবং
   SharedHttpClient-এর Math.Max(64, ...) override)।
   → প্রতিটি দাবি প্রকৃত টেস্ট ফলাফলের সাথে মিলিয়ে লেখো। যে দাবির পেছনে
     একটি চলমান, reproducible টেস্ট নেই, সেই দাবি ডকুমেন্ট থেকে সরাও।
     একই ভাবে docs/-এর STAGE5/6/7 IDM scorecard, FINAL_IDM_PARITY_MATRIX.md,
     CERTIFICATION_CLAIMS_AUDIT.md — সব "PASS" দাবি যাচাই করো এবং
     unverifiable দাবি সরাও। মিথ্যা PASS ভবিষ্যতের সব রিভিউকে বিপথগামী করে।

৫. CHANGELOG.md-এ 1.0.0 রিলিজের প্রকৃত অবস্থা লেখো।

শেষে সম্পূর্ণ dotnet test চালাও, coverage রিপোর্ট নাও, এবং কোন কোন নতুন
টেস্ট কোন বাগ কভার করছে তার একটি ম্যাপিং সহ রিপোর্ট দাও।
```

---

## 📌 শেষ কথা

**২১টি প্রম্পট, ৭টি ফেজ।** যদি সময় কম থাকে, অগ্রাধিকারের ক্রম:

1. **P1** — আজই। টোকেন এখনো লাইভ।
2. **P2, P4, P5** — এই তিনটি অননুমোদিত SUPER_ADMIN টেকওভারের তিনটি পথ বন্ধ করে।
3. **P3** — passkey disable করা (ধাপ ১ মাত্র কয়েক মিনিটের কাজ)।
4. **P7** — XSS চেইন।
5. **P16** — ভুয়া আর্টিফ্যাক্ট, কারণ এটি এখনই ব্যবহারকারীদের ক্ষতি করছে।
6. **P12** — একক বৃহত্তম গতি লাভ।

বাকিগুলো এর পরে ধীরে করলেও চলবে।

**একটি সতর্কতা:** P13 (merge pass বিলুপ্তি) সবচেয়ে ঝুঁকিপূর্ণ পরিবর্তন — এটি resume validation-এর পুরো মডেল বদলে দেয়। P10 ও P12 সম্পূর্ণ শেষ ও টেস্টেড না হলে P13-এ হাত দেবেন না, এবং অবশ্যই আলাদা ব্রাঞ্চে করবেন।