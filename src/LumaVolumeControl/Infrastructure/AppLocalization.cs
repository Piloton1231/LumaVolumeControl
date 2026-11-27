using LumaVolumeControl.Models;

namespace LumaVolumeControl.Infrastructure;

public static class AppLocalization
{
    public static string Text(string source)
    {
        var canonical = Canonical(source);
        if (Localization.CurrentLanguage == AppLanguage.Japanese) return canonical;
        return Values.TryGetValue(Localization.CurrentLanguage, out var dictionary)
            && dictionary.TryGetValue(canonical, out var translated)
                ? translated
                : Values[AppLanguage.English].GetValueOrDefault(canonical, canonical);
    }

    public static void Apply(DependencyObject root)
    {
        switch (root)
        {
            case Window window when !string.IsNullOrWhiteSpace(window.Title):
                window.Title = Text(window.Title);
                break;
            case TextBlock textBlock when !string.IsNullOrWhiteSpace(textBlock.Text):
                textBlock.Text = Text(textBlock.Text);
                break;
            case ContentControl contentControl when contentControl.Content is string content:
                contentControl.Content = Text(content);
                break;
        }

        if (root is FrameworkElement { ToolTip: string toolTip } element)
            element.ToolTip = Text(toolTip);

        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            Apply(child);
    }

    private static string Canonical(string value)
    {
        if (Values.Values.Any(dictionary => dictionary.ContainsKey(value))) return value;
        foreach (var dictionary in Values.Values)
        foreach (var pair in dictionary)
            if (string.Equals(pair.Value, value, StringComparison.Ordinal)) return pair.Key;
        return value;
    }

    private static readonly IReadOnlyDictionary<AppLanguage, Dictionary<string, string>> Values =
        new Dictionary<AppLanguage, Dictionary<string, string>>
        {
            [AppLanguage.English] = new()
            {
                ["変更した設定を保存しますか？"]="Save your changes?", ["はい"]="Yes", ["いいえ"]="No",
                ["最前面表示 ON"]="Always on top ON", ["最前面表示 OFF"]="Always on top OFF",
                ["コンパクトモードでミキサーの列を狭くする"]="Use compact mixer columns", ["コンパクトモード（Ctrl+M）"]="Compact mode (Ctrl+M)", ["標準表示に戻す（Ctrl+M）"]="Return to standard view (Ctrl+M)",
                ["音への反応感度（全ビジュアライザー）"]="Audio sensitivity (all visualizers)",
                ["アプリ名・実行名で検索"]="Search app or process name",
                ["スライダーの変更は即時保存され、起動中のアプリにも適用されます。"]="Slider changes are saved immediately and applied to running apps.",
                ["一致するアプリはありません"]="No matching apps",
                ["設定"]="Settings", ["基本設定"]="General", ["オーバーレイ"]="Overlay", ["ビジュアライザー"]="Visualizer", ["詳細設定"]="Advanced", ["このアプリについて"]="About",
                ["アプリごとの音量を覚え、次回から安全な音量で開始します。"]="Remember each app's volume and start at a comfortable level next time.",
                ["保存したアプリ音量を起動時に自動適用する"]="Apply saved app volumes automatically", ["Windows起動時に自動起動する"]="Start with Windows", ["起動時はトレイに最小化する"]="Start minimized to the tray",
                ["初めて起動したアプリの音量"]="New app volume", ["保存値がないアプリを最初に検出したときの音量です。"]="Volume used when an app is detected for the first time.", ["ヒント：ミキサーのスライダーを動かすと、アプリ名とその音量を自動で保存します。"]="Tip: Moving a mixer slider automatically saves the app name and volume.",
                ["アプリの表示言語"]="App language", ["言語は保存後すぐに反映されます。"]="The language changes immediately after saving.",
                ["音量変更を、操作中のモニター中央に滑らかに表示します。"]="Show volume changes smoothly on the active monitor.", ["音量変更時にオーバーレイを表示する"]="Show an overlay when volume changes", ["数字とバーを滑らかにアニメーションする"]="Animate the number and bar", ["小さなバージョン表示を出す"]="Show the small version label",
                ["オーバーレイの表示位置"]="Overlay position", ["対象アプリがあるモニターを基準に配置します。"]="Position it relative to the monitor containing the app.", ["画面中央"]="Center", ["上中央"]="Top center", ["下中央"]="Bottom center", ["左上"]="Top left", ["右上"]="Top right", ["左下"]="Bottom left", ["右下"]="Bottom right", ["表示時間"]="Display duration", ["バージョン表示の位置"]="Version label position", ["オーバーレイをプレビュー"]="Preview overlay",
                ["オーディオビジュアライザー"]="Audio visualizer", ["Windowsから再生中の音をリアルタイムに描画します。表示中だけ音声解析を行います。"]="Visualize Windows audio in real time. Audio is analyzed only while visible.", ["別ウィンドウを常に手前に表示する"]="Keep the separate window always on top", ["初期表示の種類"]="Default visualizer", ["ネオン・スペクトラム"]="Neon spectrum", ["スムーズ・ウェーブ"]="Smooth wave", ["アナログ2連VU"]="Dual analog VU", ["VFDデジタルメーター"]="VFD digital meter", ["LEDステレオメーター"]="LED stereo meter", ["X-Yオシロスコープ"]="X-Y oscilloscope", ["スペクトラム感度"]="Spectrum sensitivity", ["オシロスコープ感度"]="Oscilloscope sensitivity", ["X-Y波形の大きさを調整します。100%が標準です。"]="Adjust the X-Y trace size. 100% is standard.", ["描画の滑らかさ"]="Animation rate", ["標準（30 FPS）"]="Standard (30 FPS)", ["滑らか（60 FPS）"]="Smooth (60 FPS)", ["負荷を抑えるため、ミキサー表示中でもビジュアライザー画面を開いていない間は解析を停止します。"]="Audio analysis stops while the visualizer is hidden to reduce load.",
                ["ミキサーの操作感と保存データを管理します。"]="Manage mixer behavior and saved data.", ["別の場所をクリックしたらミキサーを閉じる"]="Close the mixer when it loses focus", ["マウスホイール1段の音量"]="Volume per mouse-wheel step", ["ミキサー全体のUIサイズ"]="Mixer UI size", ["文字・カード・バー・ボタンをまとめて縮小します。"]="Scale text, cards, sliders, and buttons together.", ["データ管理"]="Data management", ["保存した音量をリセット"]="Reset saved volumes", ["設定フォルダーを開く"]="Open settings folder", ["保存済みアプリ"]="Saved apps", ["次に同じアプリが音声を出したとき、この音量を自動適用します。"]="Apply this volume the next time the same app plays audio.", ["まだ保存されたアプリはありません"]="No apps have been saved yet", ["この保存値を削除"]="Delete this saved value",
                ["使い方"]="How to use", ["Luma Volume Controlを終了"]="Exit Luma Volume Control", ["キャンセル"]="Cancel", ["保存"]="Save", ["設定を保存しました"]="Settings saved", ["保存したアプリ名と音量をリセットしました。"]="Saved app names and volumes were reset.", ["選択したアプリの保存値を削除しました。"]="The selected saved value was deleted.",
                ["オーディオデバイスを確認中…"]="Checking audio device…", ["更新"]="Refresh", ["デバイス"]="Device", ["アプリ名で検索"]="Search by app name", ["アプリを検索"]="Search apps", ["Windows 全体"]="Windows master", ["デバイスをミュート"]="Mute device", ["音声を再生中のアプリはありません"]="No apps are playing audio", ["← ミキサー"]="← Mixer", ["別ウィンドウ"]="Separate window", ["ミュート切替"]="Toggle mute", ["音量を記憶済み"]="Volume remembered", ["● 自動調整 ON"]="● Auto adjust ON", ["● 自動調整 OFF"]="● Auto adjust OFF", ["ホイールで調整 · Escで閉じる · トレイ右クリックで終了"]="Wheel to adjust · Esc to close · Right-click tray to exit", ["種類を選択 · 別ウィンドウ表示に対応 · Escで閉じる"]="Choose a style · Separate window supported · Esc to close",
                ["Windows出力をリアルタイム表示"]="Real-time Windows audio", ["常に手前に表示"]="Always on top", ["閉じる"]="Close", ["音声が再生されると動きます"]="Moves while audio is playing", ["ドラッグで移動 · 右上のピンで最前面を切替"]="Drag to move · Use the pin to toggle always-on-top", ["音量を自動調整しました"]="Volume adjusted automatically", ["{0}% → {1}% にしました"]="Changed {0}% → {1}%", ["プレビュー"]="Preview",
                ["ボリュームミキサーを開く"]="Open volume mixer", ["オーディオビジュアライザーを開く"]="Open audio visualizer", ["Luma Volume Control を終了"]="Exit Luma Volume Control"
            },
            [AppLanguage.Korean] = new()
            {
                ["コンパクトモードでミキサーの列を狭くする"]="컴팩트 믹서 열 사용", ["コンパクトモード（Ctrl+M）"]="컴팩트 모드 (Ctrl+M)", ["標準表示に戻す（Ctrl+M）"]="표준 보기로 돌아가기 (Ctrl+M)", ["設定"]="설정", ["基本設定"]="기본 설정", ["オーバーレイ"]="오버레이", ["ビジュアライザー"]="비주얼라이저", ["詳細設定"]="고급 설정", ["このアプリについて"]="앱 정보", ["アプリの表示言語"]="앱 언어", ["言語は保存後すぐに反映されます。"]="저장하면 언어가 즉시 반영됩니다.", ["保存したアプリ音量を起動時に自動適用する"]="저장된 앱 음량 자동 적용", ["Windows起動時に自動起動する"]="Windows 시작 시 자동 실행", ["起動時はトレイに最小化する"]="트레이에서 최소화로 시작", ["初めて起動したアプリの音量"]="새 앱 음량", ["オーバーレイの表示位置"]="오버레이 위치", ["画面中央"]="화면 중앙", ["上中央"]="상단 중앙", ["下中央"]="하단 중앙", ["左上"]="왼쪽 위", ["右上"]="오른쪽 위", ["左下"]="왼쪽 아래", ["右下"]="오른쪽 아래", ["表示時間"]="표시 시간", ["オーバーレイをプレビュー"]="오버레이 미리보기", ["別ウィンドウを常に手前に表示する"]="별도 창을 항상 위에 표시", ["初期表示の種類"]="기본 비주얼라이저", ["ネオン・スペクトラム"]="네온 스펙트럼", ["スムーズ・ウェーブ"]="부드러운 파형", ["X-Yオシロスコープ"]="X-Y 오실로스코프", ["スペクトラム感度"]="스펙트럼 감도", ["オシロスコープ感度"]="오실로스코프 감도", ["描画の滑らかさ"]="표시 속도", ["標準（30 FPS）"]="표준 (30 FPS)", ["滑らか（60 FPS）"]="부드러움 (60 FPS)", ["データ管理"]="데이터 관리", ["保存した音量をリセット"]="저장된 음량 초기화", ["設定フォルダーを開く"]="설정 폴더 열기", ["保存済みアプリ"]="저장된 앱", ["使い方"]="사용법", ["キャンセル"]="취소", ["保存"]="저장", ["更新"]="새로 고침", ["デバイス"]="장치", ["アプリを検索"]="앱 검색", ["Windows 全体"]="Windows 전체", ["← ミキサー"]="← 믹서", ["別ウィンドウ"]="별도 창", ["● 自動調整 ON"]="● 자동 조정 ON", ["● 自動調整 OFF"]="● 자동 조정 OFF", ["Windows出力をリアルタイム表示"]="Windows 출력 실시간 표시", ["常に手前に表示"]="항상 위에 표시", ["閉じる"]="닫기", ["音量を自動調整しました"]="음량을 자동 조정했습니다", ["{0}% → {1}% にしました"]="{0}% → {1}%로 변경했습니다", ["プレビュー"]="미리보기", ["ボリュームミキサーを開く"]="볼륨 믹서 열기", ["オーディオビジュアライザーを開く"]="오디오 비주얼라이저 열기", ["Luma Volume Control を終了"]="Luma Volume Control 종료"
            },
            [AppLanguage.ChineseSimplified] = new()
            {
                ["設定"]="设置", ["基本設定"]="基本设置", ["オーバーレイ"]="叠加层", ["ビジュアライザー"]="可视化器", ["詳細設定"]="高级设置", ["このアプリについて"]="关于", ["アプリの表示言語"]="应用语言", ["言語は保存後すぐに反映されます。"]="保存后立即切换语言。", ["保存したアプリ音量を起動時に自動適用する"]="自动应用已保存的应用音量", ["Windows起動時に自動起動する"]="随 Windows 启动", ["起動時はトレイに最小化する"]="启动时最小化到托盘", ["初めて起動したアプリの音量"]="新应用音量", ["オーバーレイの表示位置"]="叠加层位置", ["画面中央"]="屏幕中央", ["上中央"]="顶部中央", ["下中央"]="底部中央", ["左上"]="左上", ["右上"]="右上", ["左下"]="左下", ["右下"]="右下", ["表示時間"]="显示时间", ["オーバーレイをプレビュー"]="预览叠加层", ["別ウィンドウを常に手前に表示する"]="单独窗口始终置顶", ["初期表示の種類"]="默认可视化器", ["ネオン・スペクトラム"]="霓虹频谱", ["スムーズ・ウェーブ"]="平滑波形", ["X-Yオシロスコープ"]="X-Y 示波器", ["スペクトラム感度"]="频谱灵敏度", ["オシロスコープ感度"]="示波器灵敏度", ["描画の滑らかさ"]="绘制速度", ["標準（30 FPS）"]="标准（30 FPS）", ["滑らか（60 FPS）"]="流畅（60 FPS）", ["データ管理"]="数据管理", ["保存した音量をリセット"]="重置已保存音量", ["設定フォルダーを開く"]="打开设置文件夹", ["保存済みアプリ"]="已保存应用", ["使い方"]="使用方法", ["キャンセル"]="取消", ["保存"]="保存", ["更新"]="刷新", ["デバイス"]="设备", ["アプリを検索"]="搜索应用", ["Windows 全体"]="Windows 总音量", ["← ミキサー"]="← 混音器", ["別ウィンドウ"]="单独窗口", ["● 自動調整 ON"]="● 自动调整 开", ["● 自動調整 OFF"]="● 自动调整 关", ["Windows出力をリアルタイム表示"]="实时显示 Windows 音频", ["常に手前に表示"]="始终置顶", ["閉じる"]="关闭", ["音量を自動調整しました"]="已自动调整音量", ["{0}% → {1}% にしました"]="已从 {0}% 调整为 {1}%", ["プレビュー"]="预览", ["ボリュームミキサーを開く"]="打开音量混音器", ["オーディオビジュアライザーを開く"]="打开音频可视化器", ["Luma Volume Control を終了"]="退出 Luma Volume Control"
            },
            [AppLanguage.ChineseTraditional] = new()
            {
                ["設定"]="設定", ["基本設定"]="基本設定", ["オーバーレイ"]="疊加顯示", ["ビジュアライザー"]="視覺化工具", ["詳細設定"]="進階設定", ["このアプリについて"]="關於", ["アプリの表示言語"]="應用程式語言", ["言語は保存後すぐに反映されます。"]="儲存後立即切換語言。", ["保存したアプリ音量を起動時に自動適用する"]="自動套用已儲存的應用程式音量", ["Windows起動時に自動起動する"]="隨 Windows 啟動", ["起動時はトレイに最小化する"]="啟動時最小化到系統匣", ["初めて起動したアプリの音量"]="新應用程式音量", ["オーバーレイの表示位置"]="疊加顯示位置", ["画面中央"]="畫面中央", ["上中央"]="上方中央", ["下中央"]="下方中央", ["左上"]="左上", ["右上"]="右上", ["左下"]="左下", ["右下"]="右下", ["表示時間"]="顯示時間", ["オーバーレイをプレビュー"]="預覽疊加顯示", ["別ウィンドウを常に手前に表示する"]="獨立視窗永遠置頂", ["初期表示の種類"]="預設視覺化工具", ["ネオン・スペクトラム"]="霓虹頻譜", ["スムーズ・ウェーブ"]="平滑波形", ["X-Yオシロスコープ"]="X-Y 示波器", ["スペクトラム感度"]="頻譜靈敏度", ["オシロスコープ感度"]="示波器靈敏度", ["描画の滑らかさ"]="繪製速度", ["標準（30 FPS）"]="標準（30 FPS）", ["滑らか（60 FPS）"]="流暢（60 FPS）", ["データ管理"]="資料管理", ["保存した音量をリセット"]="重設已儲存音量", ["設定フォルダーを開く"]="開啟設定資料夾", ["保存済みアプリ"]="已儲存應用程式", ["使い方"]="使用方法", ["キャンセル"]="取消", ["保存"]="儲存", ["更新"]="重新整理", ["デバイス"]="裝置", ["アプリを検索"]="搜尋應用程式", ["Windows 全体"]="Windows 總音量", ["← ミキサー"]="← 混音器", ["別ウィンドウ"]="獨立視窗", ["● 自動調整 ON"]="● 自動調整 開", ["● 自動調整 OFF"]="● 自動調整 關", ["Windows出力をリアルタイム表示"]="即時顯示 Windows 音訊", ["常に手前に表示"]="永遠置頂", ["閉じる"]="關閉", ["音量を自動調整しました"]="已自動調整音量", ["{0}% → {1}% にしました"]="已從 {0}% 調整為 {1}%", ["プレビュー"]="預覽", ["ボリュームミキサーを開く"]="開啟音量混音器", ["オーディオビジュアライザーを開く"]="開啟音訊視覺化工具", ["Luma Volume Control を終了"]="結束 Luma Volume Control"
            }
        };
}
