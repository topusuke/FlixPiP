# FlixPiP
**モニター一枚でもゲーム中に動画みたいよね**

## 概要
ゲーム中にオーバーレイとしてWEBページを表示できます。<br>
NetflixやPrime videoなどのDRMコンテンツも再生できます。

## 動作環境
- Windows10 64bit版の1709以降、Windows11
- Webview2がインストールされているかつ、実行できること。（Edgeがインストールされていれば問題ありません。）

## 使い方
アプリを起動したら小窓でgoogleが立ち上がります。
- Ctrl + ↑ でFlixPiPにフォーカスがあたり
- マウス右ドラッグでウィンドウ位置の変更
- Ctrl + ↓ で半透明化とマウスすり抜けモード
- Shift + ↑,↓ で透明度の変更
- Ctrl + ← でgoogleを開く
- Ctrl + → でアプリの終了

## 今後の予定
- ショートカットキーの変更を可能にする

## とぷすけについて
https://www.topusuke.com/about-me<br>
X:@topusuke<br>
なにか問題があればXのDMにて承ります。

## 免責事項
これらのソフトウェアを使用したことによる一切の責任を負いません。

# FlixPiP (EN)
**Ever wanted to watch videos while gaming with just one monitor?**

## Overview
FlixPiP allows you to display web pages as an overlay while gaming.<br>
It can also play DRM-protected content from services such as Netflix and Prime Video.

## System Requirements
- Windows 10 64-bit version 1709 or later, or Windows 11
- WebView2 must be installed and functional. (If Microsoft Edge is installed, WebView2 should be available.)

## Usage
When you launch the application, a small window will open with Google.
- Ctrl + ↑ to focus FlixPiP
- Right-click and drag to move the window
- Ctrl + ↓ to enable transparency and click-through mode
- Shift + ↑, ↓ to adjust the window opacity
- Ctrl + ← to open Google
- Ctrl + → to exit the application

## Planned Features
- Allow users to customize keyboard shortcuts

## About Topusuke
https://www.topusuke.com/about-me<br>
X: @topusuke<br>
If you encounter any issues, please contact me via DM on X.

## Disclaimer
I assume no responsibility for any damages or issues resulting from the use of this software.

# License and Policy

## License
This program and its files are licensed under the Apache License 2.0.

## Code signing policy

Free code signing provided by [SignPath.io](https://signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

Official FlixPiP release binaries are built from this repository using GitHub Actions.

- Committers and reviewers: [topusuke](https://github.com/topusuke)
- Approvers: [topusuke](https://github.com/topusuke)
- Each signing request requires manual approval.
- Only release artifacts built from the official FlixPiP repository are eligible for signing.

## Privacy policy

FlixPiP does not collect telemetry or send usage information to a server operated by the FlixPiP developer.

FlixPiP uses Microsoft Edge WebView2 to display websites. When the application starts, it connects to Google. When the user opens Google, Netflix, Prime Video, or another website, information may be transmitted to that website according to the website's own privacy policy.

WebView2 may store website data locally, including cookies, cache, login sessions, and browsing data.

FlixPiP also stores the following settings locally on the user's computer:

- Bookmarked URLs
- Shortcut-key settings
- Window size and position
- HTTP access preference

These settings are not transmitted to the FlixPiP developer.

Users can remove FlixPiP by deleting its application folder. Locally stored application settings and WebView2 browsing data may remain in the Windows user profile and can be removed separately by the user.

## Third-party components

FlixPiP uses the following third-party component:

- [Microsoft Edge WebView2](https://developer.microsoft.com/microsoft-edge/webview2/)
  - NuGet package: `Microsoft.Web.WebView2`
  - License and third-party notices:
    - [WEBVIEW2-LICENSE.txt](WEBVIEW2-LICENSE.txt)
    - [WEBVIEW2-NOTICE.txt](WEBVIEW2-NOTICE.txt)

FlixPiP uses the Evergreen WebView2 Runtime installed on the user's system. The WebView2 Runtime itself is not bundled with FlixPiP.
