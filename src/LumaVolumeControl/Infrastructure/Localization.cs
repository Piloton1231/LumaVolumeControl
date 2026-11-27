using LumaVolumeControl.Models;

namespace LumaVolumeControl.Infrastructure;

public static class Localization
{
    private static AppLanguage _language = AppLanguage.Japanese;
    public static AppLanguage CurrentLanguage => _language;
    public static void SetLanguage(AppLanguage language) => _language = language;
    public static string T(string key) => Dictionaries[_language].TryGetValue(key, out var value) ? value : Dictionaries[AppLanguage.Japanese].GetValueOrDefault(key, key);

    private static readonly IReadOnlyDictionary<AppLanguage, Dictionary<string, string>> Dictionaries =
        new Dictionary<AppLanguage, Dictionary<string, string>>
        {
            [AppLanguage.Japanese] = new()
            {
                ["Common.Cancel"]="キャンセル", ["Common.Close"]="閉じる", ["Common.Retry"]="再試行", ["Common.Delete"]="アンインストール",
                ["Installer.SetupTitle"]="Luma Volume Control セットアップ", ["Installer.InstallHeading"]="Luma Volume Controlをインストール", ["Installer.InstallDesc"]="アプリごとの音量を保存し、次回から安全な音量で開始します。",
                ["Installer.WarningTitle"]="注意事項", ["Installer.WarningText"]="初期設定では、インストール後にアプリの音量が自動で変更されます。初めて検出したアプリは30%に設定されます。不要な場合は設定から自動音量調整をOFFにできます。",
                ["Installer.Language"]="インストーラーの言語", ["Installer.Startup"]="Windowsへのサインイン時に自動起動", ["Installer.Desktop"]="デスクトップにショートカットを作成", ["Installer.Install"]="インストール",
                ["Installer.UninstallTitle"]="Luma Volume Control アンインストール", ["Installer.DeleteHeading"]="Luma Volume Controlを削除しますか？", ["Installer.DeleteDesc"]="アプリ、スタートメニューのショートカット、起動項目を削除します。保存した音量設定は残ります。",
                ["Installer.RemovingHeading"]="アンインストールしています…", ["Installer.RemovingDesc"]="アプリを安全に終了して削除しています。", ["Installer.Preparing"]="インストールを準備しています…",
                ["Installer.Installed"]="インストール完了", ["Installer.InstalledDesc"]="タスクトレイのLumaアイコンからボリュームミキサーを開けます。", ["Installer.Launch"]="Luma Volume Controlを起動",
                ["Installer.InstallFailed"]="インストールできませんでした", ["Installer.Uninstalled"]="アンインストール完了", ["Installer.UninstalledDesc"]="保存したアプリ音量と個人設定は残されています。", ["Installer.Thanks"]="ご利用ありがとうございました。", ["Installer.UninstallFailed"]="アンインストールできませんでした",
                ["Installer.ExecutableMissing"]="実行ファイルの場所を確認できません。", ["Installer.InvalidTarget"]="アンインストール先が正しくありません。", ["Installer.DeleteFolderFailed"]="アプリのフォルダーを削除できません。使用中のウィンドウを閉じて再試行してください。", ["Installer.ShortcutUnavailable"]="ショートカット機能を利用できません。", ["Installer.ShortcutDesc"]="Luma Volume Controlを起動"
            },
            [AppLanguage.English] = new()
            {
                ["Common.Cancel"]="Cancel", ["Common.Close"]="Close", ["Common.Retry"]="Retry", ["Common.Delete"]="Uninstall",
                ["Installer.SetupTitle"]="Luma Volume Control Setup", ["Installer.InstallHeading"]="Install Luma Volume Control", ["Installer.InstallDesc"]="Remember each app's volume and start it at a comfortable level next time.",
                ["Installer.WarningTitle"]="Important notice", ["Installer.WarningText"]="With the default settings, app volumes are changed automatically after installation. Newly detected apps are set to 30%. You can turn off automatic volume adjustment in Settings.",
                ["Installer.Language"]="Installer language", ["Installer.Startup"]="Start when I sign in to Windows", ["Installer.Desktop"]="Create a desktop shortcut", ["Installer.Install"]="Install",
                ["Installer.UninstallTitle"]="Uninstall Luma Volume Control", ["Installer.DeleteHeading"]="Uninstall Luma Volume Control?", ["Installer.DeleteDesc"]="The app, Start menu shortcut, and startup entry will be removed. Saved volume settings will remain.",
                ["Installer.RemovingHeading"]="Uninstalling…", ["Installer.RemovingDesc"]="Safely closing and removing the app.", ["Installer.Preparing"]="Preparing installation…",
                ["Installer.Installed"]="Installation complete", ["Installer.InstalledDesc"]="Open the volume mixer from the Luma icon in the system tray.", ["Installer.Launch"]="Launch Luma Volume Control",
                ["Installer.InstallFailed"]="Installation failed", ["Installer.Uninstalled"]="Uninstall complete", ["Installer.UninstalledDesc"]="Saved app volumes and personal settings have been kept.", ["Installer.Thanks"]="Thank you for using Luma Volume Control.", ["Installer.UninstallFailed"]="Uninstall failed",
                ["Installer.ExecutableMissing"]="The executable could not be located.", ["Installer.InvalidTarget"]="The uninstall target is invalid.", ["Installer.DeleteFolderFailed"]="The app folder could not be removed. Close any window using it and try again.", ["Installer.ShortcutUnavailable"]="Shortcut support is unavailable.", ["Installer.ShortcutDesc"]="Launch Luma Volume Control"
            },
            [AppLanguage.Korean] = new()
            {
                ["Common.Cancel"]="취소", ["Common.Close"]="닫기", ["Common.Retry"]="다시 시도", ["Common.Delete"]="제거",
                ["Installer.SetupTitle"]="Luma Volume Control 설치", ["Installer.InstallHeading"]="Luma Volume Control 설치", ["Installer.InstallDesc"]="앱별 음량을 기억하고 다음 실행부터 편안한 음량으로 시작합니다.",
                ["Installer.WarningTitle"]="주의 사항", ["Installer.WarningText"]="기본 설정에서는 설치 후 앱 음량이 자동으로 변경됩니다. 처음 감지한 앱은 30%로 설정됩니다. 설정에서 자동 음량 조정을 끌 수 있습니다.",
                ["Installer.Language"]="설치 프로그램 언어", ["Installer.Startup"]="Windows 로그인 시 자동 시작", ["Installer.Desktop"]="바탕 화면 바로 가기 만들기", ["Installer.Install"]="설치",
                ["Installer.UninstallTitle"]="Luma Volume Control 제거", ["Installer.DeleteHeading"]="Luma Volume Control을 제거할까요?", ["Installer.DeleteDesc"]="앱, 시작 메뉴 바로 가기와 시작 항목을 제거합니다. 저장한 음량 설정은 유지됩니다.",
                ["Installer.RemovingHeading"]="제거 중…", ["Installer.RemovingDesc"]="앱을 안전하게 종료하고 제거하는 중입니다.", ["Installer.Preparing"]="설치 준비 중…",
                ["Installer.Installed"]="설치 완료", ["Installer.InstalledDesc"]="시스템 트레이의 Luma 아이콘에서 볼륨 믹서를 열 수 있습니다.", ["Installer.Launch"]="Luma Volume Control 실행",
                ["Installer.InstallFailed"]="설치 실패", ["Installer.Uninstalled"]="제거 완료", ["Installer.UninstalledDesc"]="저장한 앱 음량과 개인 설정은 유지됩니다.", ["Installer.Thanks"]="이용해 주셔서 감사합니다.", ["Installer.UninstallFailed"]="제거 실패",
                ["Installer.ExecutableMissing"]="실행 파일을 찾을 수 없습니다.", ["Installer.InvalidTarget"]="제거 대상이 올바르지 않습니다.", ["Installer.DeleteFolderFailed"]="앱 폴더를 삭제할 수 없습니다. 사용 중인 창을 닫고 다시 시도하세요.", ["Installer.ShortcutUnavailable"]="바로 가기 기능을 사용할 수 없습니다.", ["Installer.ShortcutDesc"]="Luma Volume Control 실행"
            },
            [AppLanguage.ChineseSimplified] = new()
            {
                ["Common.Cancel"]="取消", ["Common.Close"]="关闭", ["Common.Retry"]="重试", ["Common.Delete"]="卸载",
                ["Installer.SetupTitle"]="Luma Volume Control 安装程序", ["Installer.InstallHeading"]="安装 Luma Volume Control", ["Installer.InstallDesc"]="记住每个应用的音量，下次以舒适的音量启动。",
                ["Installer.WarningTitle"]="注意事项", ["Installer.WarningText"]="默认设置下，安装后会自动调整应用音量。首次检测到的应用会设为30%。您可以在设置中关闭自动音量调整。",
                ["Installer.Language"]="安装程序语言", ["Installer.Startup"]="登录 Windows 时自动启动", ["Installer.Desktop"]="创建桌面快捷方式", ["Installer.Install"]="安装",
                ["Installer.UninstallTitle"]="卸载 Luma Volume Control", ["Installer.DeleteHeading"]="卸载 Luma Volume Control？", ["Installer.DeleteDesc"]="将删除应用、开始菜单快捷方式和启动项。已保存的音量设置会保留。",
                ["Installer.RemovingHeading"]="正在卸载…", ["Installer.RemovingDesc"]="正在安全关闭并删除应用。", ["Installer.Preparing"]="正在准备安装…",
                ["Installer.Installed"]="安装完成", ["Installer.InstalledDesc"]="可从系统托盘中的Luma图标打开音量混合器。", ["Installer.Launch"]="启动 Luma Volume Control",
                ["Installer.InstallFailed"]="安装失败", ["Installer.Uninstalled"]="卸载完成", ["Installer.UninstalledDesc"]="已保存的应用音量和个人设置仍会保留。", ["Installer.Thanks"]="感谢使用。", ["Installer.UninstallFailed"]="卸载失败",
                ["Installer.ExecutableMissing"]="无法找到可执行文件。", ["Installer.InvalidTarget"]="卸载目标无效。", ["Installer.DeleteFolderFailed"]="无法删除应用文件夹。请关闭正在使用它的窗口后重试。", ["Installer.ShortcutUnavailable"]="快捷方式功能不可用。", ["Installer.ShortcutDesc"]="启动 Luma Volume Control"
            },
            [AppLanguage.ChineseTraditional] = new()
            {
                ["Common.Cancel"]="取消", ["Common.Close"]="關閉", ["Common.Retry"]="重試", ["Common.Delete"]="解除安裝",
                ["Installer.SetupTitle"]="Luma Volume Control 安裝程式", ["Installer.InstallHeading"]="安裝 Luma Volume Control", ["Installer.InstallDesc"]="記住每個應用程式的音量，下次以舒適的音量啟動。",
                ["Installer.WarningTitle"]="注意事項", ["Installer.WarningText"]="預設設定下，安裝後會自動調整應用程式音量。首次偵測到的應用程式會設為30%。您可以在設定中關閉自動音量調整。",
                ["Installer.Language"]="安裝程式語言", ["Installer.Startup"]="登入 Windows 時自動啟動", ["Installer.Desktop"]="建立桌面捷徑", ["Installer.Install"]="安裝",
                ["Installer.UninstallTitle"]="解除安裝 Luma Volume Control", ["Installer.DeleteHeading"]="解除安裝 Luma Volume Control？", ["Installer.DeleteDesc"]="將移除應用程式、開始功能表捷徑和啟動項目。已儲存的音量設定會保留。",
                ["Installer.RemovingHeading"]="正在解除安裝…", ["Installer.RemovingDesc"]="正在安全關閉並移除應用程式。", ["Installer.Preparing"]="正在準備安裝…",
                ["Installer.Installed"]="安裝完成", ["Installer.InstalledDesc"]="可從系統匣中的Luma圖示開啟音量混音器。", ["Installer.Launch"]="啟動 Luma Volume Control",
                ["Installer.InstallFailed"]="安裝失敗", ["Installer.Uninstalled"]="解除安裝完成", ["Installer.UninstalledDesc"]="已儲存的應用程式音量和個人設定仍會保留。", ["Installer.Thanks"]="感謝使用。", ["Installer.UninstallFailed"]="解除安裝失敗",
                ["Installer.ExecutableMissing"]="找不到執行檔。", ["Installer.InvalidTarget"]="解除安裝目標無效。", ["Installer.DeleteFolderFailed"]="無法刪除應用程式資料夾。請關閉正在使用它的視窗後重試。", ["Installer.ShortcutUnavailable"]="捷徑功能無法使用。", ["Installer.ShortcutDesc"]="啟動 Luma Volume Control"
            }
        };
}
