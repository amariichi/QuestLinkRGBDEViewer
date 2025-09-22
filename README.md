## Quest Link RGBDE Viewer App for windows

![Image](https://github.com/user-attachments/assets/c5ea84ca-8de5-48a4-bddf-a6d4e04204c4) ![Image](https://github.com/user-attachments/assets/b11d7d0a-a5f8-4304-b8e0-cd80d649e1a6) ![Image](https://github.com/user-attachments/assets/3df33d1f-4da0-4a2d-9433-8e44e1183219)

Language / 言語: [English](#english) | [日本語](#日本語)

---

## 日本語

### 概要
付属の Python スクリプトを使用して、Apple が公開した Depth Pro で推定したデプス情報を追加したPNG画像データを作成します。
そのPNG画像を読み込み、Meta Quest 2 などで 3D 画像を観るための Meta Quest Link 用アプリです。

- **v1.1.2** : モデル拡大・縮小時に視点との距離が不自然に変化しないよう前方アンカーを導入
- **v1.1.1** : 360度画像のメッシュ向きを修正し、ドキュメントを更新
- **v1.1.0** : 球面パッチを押し込む方式から、推定デプスで各ピクセルへ視線投影したメッシュ再構築と深度ノイズ対策を導入


3D 画像用の PNG ファイルの作成に必要な Python スクリプトの実行には、別途 Depth Pro [[URL](https://github.com/apple/ml-depth-pro)] [[Checkpoints URL](https://huggingface.co/apple/DepthPro)] のインストールが必要です。

### [ファイルのダウンロード](https://github.com/amariichi/QuestLinkRGBDEViewer/releases/tag/v1.1.2)

### 設定方法及び使用方法の概要
1. Depth Pro を公式ページに記載の方法でインストール。
2. 1.で Depth Pro をインストールしたフォルダに、`depth-pro_rgbde.py` をコピーするとともに `input` フォルダを作成。
3. Quest 2 などで立体視したい画像（jpg又はpng）を `input` フォルダに入れる。
4. ターミナル上で `python depth-pro_rgbde.py` と入力してスクリプトを実行。`output` フォルダに左半分が元画像で右半分がほぼ透明[^1] の PNG ファイルが生成されます。`--sphere` オプションを付けると、ファイル名が "xxx_RGBDE **.360** .png" となります。
5. 上記のリンクからダウンロードしたアプリの実行ファイルを任意のフォルダに解凍。
6. Meta Quest Link [[URL](https://www.meta.com/ja-jp/help/quest/pcvr/)] でヘッドセットを PC に接続。
7. アプリを実行するとファイルブラウザ[^2]が立ち上がるので、4. で作成した PNG ファイルを選択すると画像が立体表示されます。ファイル名が `.360.png` となっている場合は、全天球画像として処理されます。 [Sample_Images](https://github.com/amariichi/QuestLinkRGBDEViewer/tree/main/Sample_Images) フォルダにいくつかサンプル画像が入っています。なお、ファイルブラウザの位置がおかしい場合は、右コントローラーのロゴマークボタンを押して正面の位置を修正してください。
8. 操作方法は画面の左下に表示されています（左の Hand Trigger を押すと説明の表示／非表示を切り替えられます。）。左コントローラーの Start ボタンを押すと、ファイルブラウザが起動します。また、その際、表示中の画像の位置などが初期化されます。

[^1]: Depth Pro の推定デプスの最大値は 10,000m です。このデプス情報を 10,000 倍した値を uint32 にして、8 ビットずつリトルエンディアンで RGBA に保存していますので、アルファチャンネルの値は 5 以内に収まります。このため右側はほぼ透明の画像となっています。

[^2]: ファイルブラウザには、**UnitySimpleFileBrowser** [[URL](https://github.com/yasirkula/UnitySimpleFileBrowser)] を使用しています。作者の S?leyman Yasir KULA 氏に感謝申し上げます。

### アプリの具体的な使用方法
- **Start ボタン（左コントローラー）** ファイルブラウザーを起動します。
- **左スティック** 画像の位置を前後又は左右に動かします。
- **左トリガー + コントローラー** 画像の位置を動かします。マウスのドラッグのように使用します。
- **左ハンドトリガー** 操作説明の表示／非表示を切り替えます。
- **X / Y ボタン** 画像の奥行きを調整します。奥行きが Log モードの時のみ作用します（Log モードで使用する下の数式の b を増減します。）。
```math
z' = a \times Log(1 + z^b)
```
- **右スティック** 左右で画像を拡大・縮小します。
- **右トリガー + コントローラー前後** 画像の奥行きを調整します（上の数式の a を増減します。）。
- **A ボタン** 奥行きを Log モードにします。
- **B ボタン** 奥行きを Linear モードにします。
- **右ハンドトリガー + コントローラー左右** 曲率半径を変更します。（右：曲がり具合を大きく、左：より平面に近く）。

### Unity Editor への読み込み（自分でビルドしたり改造したい方向けの説明）
- ソースファイルの内容を任意のフォルダに入れ、**`Assets` フォルダーの中に Unity-StarterSamples v71.0.0 [[URL](https://github.com/oculus-samples/Unity-StarterSamples/releases/tag/v71.0.0)] を追加します**（`Unity-StarterSamples-71.0.0` というフォルダとその中身が `Assets` フォルダに入っている状態。）。
- Unity Hub の "Add project from disk" メニューでインストールしたフォルダを指定します。
- `Assets > Scenes` から、 `Sample Scene` を Hierarchy ウインドウにドロップしてください。Unity が自動追加した Scene は削除してください。
- Unity Editor 内でもアプリは動作します（実行前に Meta Quest Link へゴーグルを接続することが必要です。）。

### Q&A
**Q: 通常の RGBD 画像との違いは何ですか。**

**A:** 通常、RGBD 画像は、元画像の右側に255階調のデプス情報を保持します。Depth Pro 付属の `run.py` では、0.1m から 250m までのデプス情報の逆数で各ピクセルのデプスを正規化し、0 から 255 までのデプスを割り当てています。この方法では、近くにある被写体のデプス情報の解像度は高いですが、例えば地面、壁、草、偶然映り込んだ小さな物体などが手前にあり、奥にメインの被写体があるような画像の場合、メインの被写体のデプス情報の階調が低くなり、凹凸の少ない出力となってしまいます（下の図を参照）。一方、このツールのスクリプトでは、元の float のデプス情報を１万倍して `uint32` で保持しており、メインの被写体にズームしてもデプスの情報が元の推定どおり維持されることから立体感が損なわれません。

![fig](https://github.com/user-attachments/assets/15175e2d-41d7-4a30-a5a5-6748065f1ff2)

---

## English

The following is an automatic translation by ChatGPT and is a provisional translation.

### Overview
Using the included Python script, this application creates a PNG image file that incorporates depth information estimated by Depth Pro, which is provided by Apple. By loading that PNG image, you can view it in 3D on devices like Meta Quest 2 via a Meta Quest Link application.

- **v1.1.2**: Added a forward anchor so scaling the model keeps the perceived viewing distance stable.
- **v1.1.1**: Fixed triangle winding for 360° panoramas and refreshed documentation.
- **v1.1.0**: Replaced the spherical push mesh with per-pixel ray reconstruction using estimated depth and added depth noise mitigation.

A separate installation of Depth Pro  [[URL](https://github.com/apple/ml-depth-pro)] [[Checkpoints URL](https://huggingface.co/apple/DepthPro)] is required to run the Python script necessary for creating PNG files for 3D images.

### [Download Files](https://github.com/amariichi/QuestLinkRGBDEViewer/releases/tag/v1.1.2)

### Outline of Setup and Usage
1. Install Depth Pro following the instructions on the official page.
2. Copy `depth-pro_rgbde.py` to the folder where Depth Pro was installed, and create an `input` folder there as well.
3. Place the images (jpg or png) you want to view in stereoscopic 3D (e.g., on Quest 2) into the `input` folder.
4. Run the script by typing `python depth-pro_rgbde.py` in the terminal. A PNG file whose left half is the original image and whose right half is almost transparent[^3] will be generated in the `output` folder. When you use the `--sphere` option, the filename becomes xxx_RGBDE **.360** .png.
5. Unzip the downloaded application executable (from the link above) into any folder of your choice.
6. Connect your headset (such as Meta Quest 2) to your PC via Meta Quest Link [[URL](https://www.meta.com/ja-jp/help/quest/pcvr/)].
7. When you run the application, a file browser[^4] will appear. Select the PNG file you created in step 4 to view the image in 3D. In addition, if a filename ends with `.360.png`, it will be treated as a full-sphere image. Several sample images are included in the [Sample_Images](https://github.com/amariichi/QuestLinkRGBDEViewer/tree/main/Sample_Images) folder. If the file browser window appears in an odd position, press the logo-mark button on the right controller to reset the forward-facing direction.
8. The operation instructions are shown at the bottom-left of the screen. Press the left controller’s Start button to open the file browser. This also resets the position of the currently displayed image.

[^3]: The maximum depth value estimated by Depth Pro is 10,000m. We multiply the original estimated depth value by 10,000, store it as a uint32, and then split it into 8-bit segments in little-endian order to save it as RGBA. As a result, alpha channel values are limited to 5 or below, making the right half of the image nearly transparent.

[^4]: The file browser uses **UnitySimpleFileBrowser** [[URL](https://github.com/yasirkula/UnitySimpleFileBrowser)]. I extend my gratitude to its creator, S?leyman Yasir KULA

### Detailed Usage of the Application
 - **Start Button (Left Controller)** Opens the file browser.
 - **Left Joystick** Moves the image forward/back or left/right.
 - **Left Trigger + Controller** Moves the image, functioning like dragging with a mouse.
 - **Left Hand Trigger** Toggles the display of the operation instructions.
 - **X / Y Buttons** Adjust the depth of the image. These only work in Log mode (they increase or decrease parameter b in the formula below):
```math
z' = a \times Log(1 + z^b)
```
 - **Right Joystick** Moves left or right to zoom the image in or out.
 - **Right Trigger + Controller** Moves the image’s depth forward or backward (adjusts the parameter a in the formula above).
 - **A Button** Switches the depth mode to Log.
 - **B Button** Switches the depth mode to Linear.
 - **Right Hand Trigger + Controller (Left/Right)** Adjusts the curvature radius (moving right increases the curvature, while moving left makes it closer to a flat plane).

### Loading into Unity Editor (for those who wish to build or modify it themselves)
 - Copy the source files into any folder, then **add Unity-StarterSamples v71.0.0 [[URL](https://github.com/oculus-samples/Unity-StarterSamples/releases/tag/v71.0.0)] into the `Assets` folder** (so that a folder named Unity-StarterSamples-71.0.0 and its contents are inside `Assets`).
 - In Unity Hub, use "Add project from disk" and select the folder where you placed the files.
From `Assets > Scenes`, drag `Sample Scene` into the Hierarchy window. Remove any automatically added Scenes from Unity.
 - The application can run within the Unity Editor as well (you need to connect the headset to Meta Quest Link before running).

### Q&A
**Q: What is the difference from a typical RGBD image?**

**A:** Typically, an RGBD image includes 8-bit (0?255) depth information on the right side of the original image. In the run.py script provided with Depth Pro, depth values from 0.1m to 250m are normalized by taking the reciprocal and assigning a range from 0 to 255. While this gives high-resolution depth data for close-up subjects, if there are objects like ground, walls, grass, or small items in the foreground, the main subject in the background will have a lower depth range, resulting in reduced 3D details (see the figure below). In contrast, this tool’s script multiplies the original floating-point depth values by 10,000 and stores them in uint32. As a result, even when zooming in on the main subject, the originally estimated depth remains intact, preserving a strong sense of 3D depth.

![fig](https://github.com/user-attachments/assets/15175e2d-41d7-4a30-a5a5-6748065f1ff2)
