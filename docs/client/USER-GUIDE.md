# دليل المستخدم — نظام إدارة ترخيص السواقين والمركبات (DVLD)

> **الجمهور المستهدف:** موظفو الكاونتر (`Staff`) ومسؤولو النظام (`Admin`)  
> **نطاق الدليل:** شرح استخدام بوابة الويب (`ClientApp`) والصفحات والمسارات الـ 29 وإجراءات العمل المتوفرة فعليًا في الواجهة الحالية (`e9e843e`).

---

## 1. نظرة عامة وأدوار المستخدمين

بوابة DVLD مخصصة لموظفي دائرة الترخيص الداخليين ولا يمتلك المواطنون حسابات دخول مباشرة عليها. تنقسم الصلاحيات في النظام بين دورين رئيسيين:

| الدور | المهام والصفحات المتاحة في البوابة | القيود والتنبيهات المعتمدة |
|---|---|---|
| **موظف الكاونتر (`Staff`)** | لوحة المؤشرات (`Dashboard`)، إدارة الأشخاص (`People`)، السائقون (`Drivers`)، تاريخ رخص الشخص (`Person License History`)، طلبات الرخص المحلية (`Local Applications`)، مواعيد ونتائج الفحوصات (`Test Appointments`)، الرخص المحلية (`Licenses`)، تجديد واستبدال الرخص (`Renew & Replace License`)، حجز وتحرير الرخص (`License Detention`)، الرخص الدولية (`International Licenses`)، وعرض الإعدادات المرجعية للقراءة فقط، إضافة إلى الملف الشخصي وتغيير كلمة المرور. | لا يمكنه فتح شاشة إدارة المستخدمين (`User Management`) ولا تعديل رسوم أو إعدادات أنواع الطلبات وفئات الرخص وأنواع الفحوصات. |
| **مسؤول النظام (`Admin`)** | لوحة المؤشرات (`Dashboard`)، إدارة المستخدمين (`User Management`)، إدارة الأشخاص (`People`)، عرض وتعديل الإعدادات (`Application Types`، `License Classes`، `Test Types`)، عرض الدول (`Countries`)، والملف الشخصي وتغيير كلمة المرور. | وفق سياسة فصل المهام (`ADR-O01` Model B)، لا ينفذ مسؤول النظام معاملات الكاونتر التشغيلية. **تنبيه تعارض صلاحيات (`SEC-01` / `RSK-12`):** تتيح قائمة الواجهة (`PAGE_PERMISSIONS`) صفحة `Person License History` لدور `Admin` أيضًا، لكن نقاط API الخلفية (`LicensesController` و`InternationalLicensesController`) مقيدة بـ `StaffOnly` وتعيد `HTTP 403 Forbidden` لمسؤول النظام. |

> **ملاحظة:** إذا حاول مستخدم فتح صفحة غير مصرح له بها، تعرض الواجهة شاشة **عدم توفر الصلاحية (`Access Denied`)** مع زر للعودة إلى لوحة المؤشرات.

---

## 2. تسجيل الدخول وإدارة الجلسة

1. **تسجيل الدخول (`Sign in`):**
   - أدخل اسم المستخدم (`Username`) وكلمة المرور (`Password`) في شاشة الدخول (`Login`).
   - يمكنك استخدام أيقونة إظهار/إخفاء كلمة المرور للتحقق من الإدخال.
   - خيار **تذكرني (`Remember me`)** يحفظ اسم المستخدم فقط محليًا على المتصفح تحت المفتاح `dvld_remembered_username` لتسهيل الدخول لاحقًا، ولا يحفظ كلمة المرور أو رموز الجلسة (حيث يُحفظ رمز الوصول JWT في الذاكرة فقط).
   - **نسيت كلمة المرور (`Forgot password?`):** زر معلوماتي فقط (لا يوجد مسار استعادة ذاتي عبر البريد أو الرسائل)؛ عند الضغط عليه يعرض الرسالة التالية: `"Password reset is managed administratively. Please contact your DVLD Systems Administrator."`
2. **أثناء الجلسة، تعدد التبويبات، وتسجيل الخروج:**
   - تتجدد جلسة العمل تلقائيًا عبر كوكي `HttpOnly` (`dvld_rt`) ضمن حدود الجلسة الفعالة (14 يومًا كحد أقصى أو 7 أيام خمول).
   - إذا انتهت صلاحية الجلسة أو تم تعطيل الحساب أو تغيير كلمة المرور أو الدور، يرفض الخادم الطلبات فورًا عبر `OnTokenValidated` ويتم تسجيل خروجك وتوجيهك لشاشة الدخول مع رسالة تنبيهية.
   - **تسجيل الخروج (`Sign out`):** يفتح نافذة تأكيد (`ConfirmModal`) من القائمة الجانبية أو قائمة الحساب في الشريط العلوي:
     - ينسّق `BroadcastChannel('dvld-auth')` مسح الجلسة المحلية وإعادة جميع التبويبات المفتوحة على نفس المتصفح والأصل إلى شاشة الدخول فورًا.
     - يُرسل طلب إبطال جلسة الخادم (`POST /api/Auth/logout`) بصورة *best-effort* دون انتظار فحص حالة الاستجابة؛ لذا في حال فشل الاتصال بالخادم أثناء الخروج قد يبقى كوكي التحديث صالحًا وتُستعاد الجلسة عند إعادة تحميل الصفحة لاحقًا (نقطة متابعة موثقة). كما لا يوجد حاليًا زر مستقل في الواجهة لاستدعاء `POST /api/Auth/logout-all` يدويًا رغم وجوده على مستوى الخادم و`AuthContext`.
3. **تغيير كلمة المرور (`Change Password`):**
   - تعرض الشاشة قائمة تحقق تفاعلية لسياسة كلمة المرور (طول `8–100` حرف، مختلفة عن الحالية، ومطابقة لحقل التأكيد).
   - يتم التحديث على خطوتين: يعتمد الخادم تغيير كلمة المرور ويبطل جميع الجلسات السابقة أولًا، ثم يحاول تأسيس جلسة جديدة للجهاز الحالي. إذا نجح تغيير كلمة المرور وتعذّر تأسيس الجلسة الجديدة، تفرغ الشاشة الحقول وتُنفّذ خروجًا آمنًا (`logout`) لتسجيل الدخول يدويًا بكلمة المرور الجديدة.

---

## 3. التنقل والبحث السريع ومصفوفة الصفحات والمسارات

- **القائمة الجانبية (`Sidebar`):** تعرض فقط الأقسام والصفحات المسموح بها لدورك الوظيفي (`Dashboard`، `User Management` لـ `Admin`، `Directory`، `Applications & Tests`، `License Services`، `Settings`، `Account`).
- **لوحة الأوامر السريعة (`Ctrl + K`):** اضغط `Ctrl + K` (أو شريط البحث في الشريط العلوي) للانتقال الفوري إلى الصفحات المتاحة لدورك عبر البحث والأسهم و`Enter`.

### 3.1 مصفوفة الصفحات والمسارات والأدوار الفعلية (`29` مسارًا + نوافذ منبثقة)

| الصفحة (`PageId`) | المسار (`PAGE_TO_PATH`) | ظهورها في القائمة / البحث | صلاحية الواجهة (`permissions.ts`) | صلاحية API الفعلية | نوع العرض والإجراءات (`Modal` / `Page` / `Tab`) |
|---|---|---|---|---|---|
| `Dashboard` | `/` | Sidebar + `Ctrl+K` | `Admin`, `Staff` | `[Authorize]` | صفحة رئيسية ببطاقات إحصائية وروابط سريعة |
| `User Management` | `/users` | Sidebar + `Ctrl+K` | `Admin` | `AdminOnly` | صفحة جدول + نوافذ منبثقة: `AddUserModal`، `EditUserModal`، `UserDetailsModal`، `ConfirmModal` |
| `People` | `/people` | Sidebar + `Ctrl+K` | `Admin`, `Staff` | `StaffOrAdmin` | صفحة جدول مرقّم + حذف عبر `ConfirmModal` وانتقال لمسارات الأشخاص |
| `Add Person` | `/people/add` | `Ctrl+K` / من `People` و`AddUserModal` | `Admin`, `Staff` | `StaffOrAdmin` | صفحة مستقلة لإضافة شخص ورفع صورته |
| `Edit Person` | `/people/edit` | مسار مرتبط (`?personId=...`) | `Admin`, `Staff` | `StaffOrAdmin` | صفحة مستقلة لتعديل بيانات الشخص وصورته |
| `Person Details` | `/people/details` | مسار مرتبط (`?personId=...`) | `Admin`, `Staff` | `StaffOrAdmin` | صفحة تفاصيل الشخص (مع معاينة الصورة) |
| `Drivers` | `/drivers` | Sidebar + `Ctrl+K` | `Staff` | `StaffOnly` | صفحة جدول السائقين + روابط إلى تفاصيل الشخص وتاريخ الرخص |
| `Person License History` | `/licenses/history` | مسار مرتبط (`?personId=...` / `?driverId=...`) | `Staff`, `Admin` *(واجهة فقط)* | `StaffOnly` *(يعيد 403 لـ `Admin`)* | صفحة مستقلة تعرض الرخص المحلية والدولية للشخص |
| `Local Applications` | `/local-applications` | Sidebar + `Ctrl+K` | `Staff` | `StaffOnly` | صفحة جدول + تبديل داخلي لوضع الإنشاء (`view.mode='create'`) + نوافذ `ApplicationDetailsModal` و`IssueDrivingLicenseModal` و`LicenseDetailsModal` و`ConfirmModal` |
| `Test Appointments` | `/test-appointments` | Sidebar + `Ctrl+K` | `Staff` | `StaffOnly` | صفحة جدول بتبويبات (`All` / `Today` / `History`) + تأكيد الإلغاء بـ `ConfirmModal` |
| `Manage Test Appointments` | `/test-appointments/manage` | مسار مرتبط (`?localAppId=...`) | `Staff` | `StaffOnly` | صفحة مستقلة لإدارة مواعيد فحوصات طلب محلي محدد + `ConfirmModal` للإلغاء |
| `Written Test Appointments` | `/test-appointments/written` | مسار بديل (متوافق مع `Manage Test Appointments`) | `Staff` | `StaffOnly` | يفتح نفس مكون `ManageTestAppointmentsPage` عند الرجوع/المسار المرتبط |
| `Appointment Details` | `/test-appointments/details` | مسار مرتبط (`?appointmentId=...`) | `Staff` | `StaffOnly` | صفحة مستقلة لتفاصيل الموعد + `ConfirmModal` للإلغاء |
| `Schedule Appointment` | `/test-appointments/schedule` | مسار مرتبط (`?localAppId=...&testTypeId=...`) | `Staff` | `StaffOnly` | صفحة مستقلة لحجز أو إعادة جدولة موعد فحص |
| `Take Examination` | `/test-appointments/take-exam` | مسار مرتبط (`?appointmentId=...`) | `Staff` | `StaffOnly` | صفحة مستقلة لتسجيل نتيجة الفحص (`Pass`/`Fail`) وقفل الموعد |
| `International Licenses` | `/international-licenses` | Sidebar + `Ctrl+K` | `Staff` | `StaffOnly` | صفحة جدول + `LicenseDetailsModal` + تبديل داخلي لشاشة `IssueInternationalLicensePage` عبر زر الإصدار |
| `Licenses` | `/licenses` | Sidebar + `Ctrl+K` | `Staff` | `StaffOnly` | صفحة سجل الرخص المحلية + `LicenseDetailsModal` + روابط للتجديد/الاستبدال/الحجز |
| `Renew & Replace License` | `/renew-replace-license` | Sidebar + `Ctrl+K` | `Staff` | `StaffOnly` | صفحة موحّدة بتبويبين (`Renew License` / `Replace License`) |
| `Renew License` | `/renew-license` | مسار مباشر للتبويب | `Staff` | `StaffOnly` | يفتح `LicenseRenewalReplacementPage` على تبويب التجديد |
| `Replace License` | `/replace-license` | مسار مباشر للتبويب | `Staff` | `StaffOnly` | يفتح `LicenseRenewalReplacementPage` على تبويب الاستبدال |
| `License Detention` | `/license-detention` | Sidebar + `Ctrl+K` | `Staff` | `StaffOnly` | صفحة موحّدة بتبويبين (`Detained Licenses` / `Detain License`) + `ReleaseDetainedLicenseModal` |
| `Detained Licenses` | `/detained-licenses` | مسار مباشر للتبويب | `Staff` | `StaffOnly` | يفتح `LicenseDetentionPage` على تبويب سجل المحجوزة + `ReleaseDetainedLicenseModal` |
| `Detain License` | `/detain-license` | مسار مباشر للتبويب | `Staff` | `StaffOnly` | يفتح `LicenseDetentionPage` على تبويب حجز الرخصة |
| `Application Types` | `/application-types` | Sidebar (Settings) + `Ctrl+K` | `Admin`, `Staff` | `GET`: `[Authorize]`, `PUT`: `AdminOnly` | صفحة جدول مع تعديل مضمّن (`inline row edit`) حصريًا لـ `Admin` |
| `License Classes` | `/license-classes` | Sidebar (Settings) + `Ctrl+K` | `Admin`, `Staff` | `GET`: `[Authorize]`, `PUT`: `AdminOnly` | صفحة جدول مع تعديل مضمّن (`inline row edit`) حصريًا لـ `Admin` |
| `Test Types` | `/test-types` | Sidebar (Settings) + `Ctrl+K` | `Admin`, `Staff` | `GET`: `[Authorize]`, `PUT`: `AdminOnly` | صفحة جدول مع تعديل مضمّن (`inline row edit`) حصريًا لـ `Admin` |
| `Countries` | `/countries` | Sidebar (Settings) + `Ctrl+K` | `Admin`, `Staff` | `GET` فقط (`[Authorize]`) | قائمة مرجعية **للقراءة فقط لجميع الأدوار** (دون تعديل أو إضافة) |
| `My Profile` | `/profile` | Sidebar + `Ctrl+K` | `Admin`, `Staff` | `[Authorize]` | صفحة عرض الملف الشخصي للموظف |
| `Change Password` | `/change-password` | Sidebar + `Ctrl+K` | `Admin`, `Staff` | `[Authorize]` | صفحة تغيير كلمة المرور (`POST /api/Auth/change-password`) |

---

## 4. دليل صفحات وإجراءات النظام

### 4.1 لوحة المؤشرات (`Dashboard`)
- تعرض ملخص الإحصاءات العامة في النظام مع روابط سريعة للوصول إلى المعاملات اليومية المتاحة لدورك.

### 4.2 إدارة الأشخاص (`People`)
- البحث في سجل المواطنين/المقيمين وتصفح الجدول المرقّم (`People`).
- الانتقال إلى صفحات عرض تفاصيل الشخص (`Person Details`)، إضافة شخص جديد (`Add Person`)، أو تعديل بياناته (`Edit Person`) بما في ذلك اختيار الجنسية ورفع أو حذف الصورة الشخصية؛ بينما يفتح إجراء الحذف نافذة تأكيد (`ConfirmModal`).

### 4.3 السائقون (`Drivers`) وتاريخ الرخص (`Person License History`)
- تتيح صفحة **السائقين (`Drivers`)** لموظفي الكاونتر (`Staff`) استعراض السائقين المسجلين والانتقال إلى تفاصيل الشخص أو تاريخ الرخص.
- تعرض صفحة **تاريخ رخص الشخص (`Person License History`)** سجل الرخص المحلية والدولية المرتبطة بالشخص، علمًا أن استدعاءاتها الخلفية مقيدة بدور `Staff` (`LicensesController` / `InternationalLicensesController`) وتعود برفض `403` إذا فتحها `Admin` عبر الواجهة.

### 4.4 طلبات رخص القيادة المحلية (`Local Applications`)
- استعراض الطلبات المحلية مع الفلترة حسب الحالة والبحث.
- إنشاء طلب رخصة قيادة محلية جديد عبر التبديل لوضع الإنشاء داخل الصفحة (يشترط وجود سجل للشخص واستيفاء الحد الأدنى للعمر المطلوب لفئة الرخصة، ولا يُسمح بوجود أكثر من طلب نشط `New` لنفس الشخص ونفس الفئة في الوقت نفسه).
- عرض التفاصيل (`ApplicationDetailsModal`)، وإصدار الرخصة لأول مرة بعد اجتياز الفحوصات الثلاثة (`IssueDrivingLicenseModal`) لتتحول حالة الطلب تلقائيًا إلى `Completed`، وعرض الرخصة (`LicenseDetailsModal`)، أو إلغاء/حذف الطلب (`ConfirmModal`).

### 4.5 مواعيد الفحوصات وتسجيل النتائج (`Test Appointments`)
يتبع التقدم في الفحوصات تسلسلًا إلزاميًا من ثلاث مراحل:
1. **فحص النظر (`Vision Test`)**
2. **الفحص النظري (`Written Test`)**
3. **الفحص العملي (`Practical / Street Test`)**

- لا يمكن حجز موعد الفحص التالي قبل اجتياز الفحص السابق بنجاح.
- لا يمكن وجود أكثر من موعد نشط غير مقفل لنفس نوع الفحص للطلب نفسه.
- عبر صفحات **`Manage Test Appointments`** و**`Schedule Appointment`** و**`Take Examination`** و**`Appointment Details`**:
  - يتم حجز موعد جديد أو إعادة جدولة موعد فحص أو إلغاؤه.
  - عند تسجيل نتيجة الفحص (**نجاح `Pass`** أو **رسوب `Fail`**)، يُقفل الموعد ويصبح غير قابل للتعديل، وعند الرسوب يمكن حجز موعد إعادة فحص (**Retake Test**).

### 4.6 خدمات الرخص (`Licenses`، `Renew & Replace License`، `License Detention`)
- **سجل الرخص (`Licenses`):** استعراض الرخص المحلية الصادرة وتفاصيل كل رخصة في `LicenseDetailsModal`.
- **تجديد الرخصة (`Renew License`):** تجديد رخصة منتهية ومستوفية للشروط (يُمنع التجديد إذا كانت الرخصة محجوزة حجزًا نشطًا ويجب تحريرها أولًا).
- **استبدال رخصة تالفة أو مفقودة (`Replace License`):** إصدار بدل تالف أو بدل فاقد لرخصة فعالة وغير منتهية. *(ملاحظة تنظيمية: سياسة استبدال الرخصة المحجوزة قيد المراجعة الرسمية `DEC-POL-01` / `ADR-O02` ولم يُفرض حظر قانوني معتمد لها في التطبيق الحالي حتى صدور تأكيد الجهة المختصة.)*
- **حجز وتحرير الرخص (`License Detention` / `Detained Licenses` / `Detain License`):**
  - حجز رخصة قيادة فعالة مع تسجيل قيمة الغرامة من تبويب `Detain License`.
  - متابعة سجل الرخص المحجوزة وتنفيذ إجراء تحرير الرخصة عبر نافذة `ReleaseDetainedLicenseModal` في تبويب `Detained Licenses`.

### 4.7 الرخص الدولية (`International Licenses`)
- استعراض سجل رخص القيادة الدولية الصادرة (`International Licenses`) وعرض التفاصيل في `LicenseDetailsModal`.
- التبديل إلى شاشة `Issue International License` لإصدار رخصة قيادة دولية جديدة بعد التحقق من أهلية الرخصة المحلية الفعالة.

### 4.8 إدارة المستخدمين (`User Management` — لمسؤول النظام `Admin` فقط)
- استعراض حسابات المستخدمين مع فتح `AddUserModal` للإضافة، `EditUserModal` للتعديل وتغيير التفعيل أو الدور (`Admin` / `Staff`)، `UserDetailsModal` لعرض التفاصيل، و`ConfirmModal` للحذف (مع حماية عدم حذف/تعطيل آخر مدير نشط أو تعديل المستخدم لنفسه، إضافةً لوجود شرط حماية خاص بالحساب الأساسي في الكود المرجعي).

### 4.9 إعدادات النظام (`Settings`)
- **`Countries`:** قائمة مرجعية للدول والجنسيات (**للقراءة فقط لجميع الأدوار**).
- **`Application Types` / `License Classes` / `Test Types`:** عرض القوائم متاح لكلا الدورين (`[Authorize]`)، بينما تقتصر صلاحية التعديل المضمّن (`PUT`) على دور **`Admin` فقط** (`AdminOnly`).

### 4.10 الحساب الشخصي (`My Profile` و`Change Password`)
- **`My Profile`:** عرض بيانات الموظف الشخصية المرتبطة بحسابه (`GET /api/Auth/profile`).
- **`Change Password`:** تحديث كلمة المرور الحالية مع التوجيه التلقائي لإعادة تسجيل الدخول إذا تعذّر إنشاء الجلسة الجديدة بعد حفظ كلمة المرور.

---

## 5. حدود هذا الدليل

- يستند هذا الدليل إلى الشاشات والمسارات وصلاحيات الواجهة والخلفية الفعلية الموجودة عند الالتزام `e9e843e`.
- لا يتضمن هذا الدليل لقطات شاشة حية أو قيم الرسوم القانونية المعتمدة في بيئة الإنتاج، إذ تعتمد القيم التشغيلية الفعلية على البيانات المضبوطة في قاعدة بيانات الجهة.
