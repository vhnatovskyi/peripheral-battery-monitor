$uk=@{
 'Devices'='Пристрої'; '{0} connected'='Підключено: {0}'
 'General'='Загальні'; 'Appearance, language and notifications.'='Вигляд, мова та сповіщення.'
 'Start with Windows'='Запускати з Windows'; 'Check for releases daily'='Перевіряти релізи раз на добу'
 'Tray icons'='Іконки біля годинника'; 'Combined (up to 4)'='Спільна (до 4 пристроїв)'; 'Separate icons'='Окремі іконки'
 'Battery symbols'='Символи батарей'; 'Low battery notifications'='Сповіщати про низький заряд'
 'Warn when charge reaches the threshold.'='Попереджати при досягненні порогу.'; 'Threshold (%)'='Поріг (%)'
 'Refresh'='Оновити'; 'Settings'='Налаштування'; 'Updates'='Релізи'; 'About'='Про програму'
 'Your devices. One place for battery status.'='Заряд ваших пристроїв в одному місці.'
 'Discovering connected devices...'='Пошук підключених пристроїв...'; 'No devices connected'='Немає підключених пристроїв'
 'Open Peripheral Battery Monitor'='Відкрити Peripheral Battery Monitor'; 'Refresh batteries'='Оновити заряд'
 'Check for updates...'='Оновлення та відкат...'; 'Exit'='Вийти'; 'Make it yours'='Налаштуйте під себе'
 'Start when I sign in to Windows'='Запускати разом із Windows'; 'Notify me when a battery is low'='Сповіщати про низький заряд'
 'Low battery threshold (%)'='Поріг низького заряду (%)'; 'Check for new releases at startup (once a day)'='Перевіряти релізи при запуску (раз на добу)'
 'Connected devices appear automatically.'='Підключені пристрої зʼявляються автоматично.'
 'Tray appearance'='Вигляд індикатора'; 'Battery rings'='Батареї пристроїв'; 'Lowest percentage'='Найменший відсоток'
 'Tray mode'='Режим іконок'; 'Combined icon (up to 4 devices)'='Спільна іконка (до 4 пристроїв)'; 'Separate icon for each device'='Окрема іконка на кожен пристрій'
 'Language'='Мова'; 'Save settings'='Зберегти'; 'Could not save settings'='Не вдалося зберегти налаштування'
 'About Peripheral Battery Monitor'='Про Peripheral Battery Monitor'; 'Close'='Закрити'
 'Developer on GitHub'='Розробник на GitHub'; 'Source code and releases'='Код програми та релізи'
 'Connected'='Підключено'; 'Low battery'='Низький заряд'; 'Battery report unavailable'='Дані про заряд недоступні'
 'Live Bluetooth battery report'='Заряд через Bluetooth'; 'Windows battery data'='Дані заряду Windows'
 'Connected; battery unavailable'='Підключено; заряд недоступний'; 'Waiting for battery'='Очікування даних заряду'
 'Mouse offline or asleep'='Миша вимкнена або спить'; 'Keyboard unavailable'='Клавіатура недоступна'
 '2.4 GHz receiver'='Приймач 2.4 GHz'; 'USB / receiver'='USB / приймач'
 'Updated {0}'='Оновлено {0}'; '{0} devices connected  /  Live battery status'='Підключено: {0}  /  Актуальний заряд'
 'Some device information is unavailable.'='Частина даних недоступна.'
 'Version {0}'='Версія {0}'; 'Developed by Vladyslav Hnatovskyi'='Розробник: Vladyslav Hnatovskyi'
 'Battery status for your wireless devices.'='Заряд ваших бездротових пристроїв.'
 'Update available'='Доступне оновлення'; 'Version {0} is available. Open Updates to install it.'='Доступна версія {0}. Відкрийте «Релізи» для встановлення.'
 'Install version {0}...'='Встановити версію {0}...'; 'Unavailable'='Недоступно'
 'Releases and rollback'='Релізи та відкат'; 'Installed version: {0}'='Встановлена версія: {0}'
 'Choose a release'='Оберіть реліз'; 'Install selected release'='Встановити вибраний реліз'; 'Cancel'='Скасувати'
 'Current'='Поточна'; 'Older version'='Старіша версія'; 'Newer version'='Новіша версія'
 'No compatible releases found.'='Сумісних релізів не знайдено.'; 'No release notes.'='Опис релізу відсутній.'
 'Install version {0}? The application will restart. Your settings will be kept.'='Встановити версію {0}? Програма перезапуститься. Налаштування збережуться.'
 'Rollback to {0}? Features from newer versions may be unavailable. The application will restart.'='Повернутися до {0}? Функції новіших версій можуть бути недоступні. Програма перезапуститься.'
 'Installed version {0}.'='Встановлено версію {0}.'; 'Could not update. {0}'='Не вдалося встановити реліз. {0}'
 'Downloading and verifying release...'='Завантаження та перевірка релізу...'
 'Disconnect'='Відʼєднати'; 'Disconnect Bluetooth device'='Відʼєднати Bluetooth-пристрій'
 'Disconnect {0} from Windows? Pairing will be kept. The device may reconnect automatically.'='Відʼєднати {0} від Windows? Спарювання збережеться. Пристрій може автоматично підключитися знову.'
 'Disconnect request sent.'='Запит на відʼєднання надіслано.'
 'Windows could not disconnect this device (error {0}). Open Bluetooth settings?'='Windows не зміг відʼєднати пристрій (помилка {0}). Відкрити налаштування Bluetooth?'
 'This connection cannot be disconnected through Bluetooth.'='Цей пристрій не використовує Bluetooth-зʼєднання.'
}
function Get-PbmText([string]$Text,[string]$Language='uk') {
    if($Language -eq 'uk' -and $uk.ContainsKey($Text)){return $uk[$Text]}
    return $Text
}
function Set-PbmControlLanguage($Control,[string]$Language){
    if($Control.Text -and $Control.Tag -isnot [string]){$Control.Tag=$Control.Text}
    if($Control.Tag -is [string]){$Control.Text=Get-PbmText $Control.Tag $Language}
    if($Control -is [Windows.Forms.ToolStrip]){foreach($item in $Control.Items){
        if($item.Tag -isnot [string]){$item.Tag=$item.Text};$item.Text=Get-PbmText $item.Tag $Language
    }}
    foreach($child in $Control.Controls){Set-PbmControlLanguage $child $Language}
}
Export-ModuleMember -Function Get-PbmText,Set-PbmControlLanguage
