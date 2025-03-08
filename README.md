## Quest Link RGBDE Viewer App for windows

![Image](https://github.com/user-attachments/assets/c5ea84ca-8de5-48a4-bddf-a6d4e04204c4) ![Image](https://github.com/user-attachments/assets/b11d7d0a-a5f8-4304-b8e0-cd80d649e1a6) ![Image](https://github.com/user-attachments/assets/3df33d1f-4da0-4a2d-9433-8e44e1183219)

### �T�v
�t���� Python �X�N���v�g���g�p���āAApple �����J���� Depth Pro �Ő��肵���f�v�X����ǉ�����PNG�摜�f�[�^���쐬���܂��B
����PNG�摜��ǂݍ��݁AMeta Quest 2 �Ȃǂ� 3D �摜���ς邽�߂� Meta Quest Link �p�A�v���ł��B

- **v1.0.1** : �v�Z�~�X���C��


3D �摜�p�� PNG �t�@�C���̍쐬�ɕK�v�� Python �X�N���v�g�̎��s�ɂ́A�ʓr Depth Pro [[URL](https://github.com/apple/ml-depth-pro)] �̃C���X�g�[�����K�v�ł��B

### [�t�@�C���̃_�E�����[�h](https://github.com/amariichi/QuestLinkRGBDEViewer/releases/tag/v1.0.1)

### �ݒ���@�y�юg�p���@�̊T�v
1. Depth Pro �������y�[�W�ɋL�ڂ̕��@�ŃC���X�g�[���B
2. 1.�� Depth Pro ���C���X�g�[�������t�H���_�ɁA`depth-pro_rgbde.py` ���R�s�[����ƂƂ��� `input` �t�H���_���쐬�B
3. Quest 2 �Ȃǂŗ��̎��������摜�ijpg����png�j�� `input` �t�H���_�ɓ����B
4. �^�[�~�i����� `python depth-pro_rgbde.py` �Ɠ��͂��ăX�N���v�g�����s�B`output` �t�H���_�ɍ����������摜�ŉE�������قړ���[^1] �� PNG �t�@�C������������܂��B`--sphere` �I�v�V������t����ƁA�t�@�C������ "xxx_RGBDE **.360** .png" �ƂȂ�܂��B
5. ��L�̃����N����_�E�����[�h�����A�v���̎��s�t�@�C����C�ӂ̃t�H���_�ɉ𓀁B
6. Meta Quest Link [[URL](https://www.meta.com/ja-jp/help/quest/pcvr/)] �Ńw�b�h�Z�b�g�� PC �ɐڑ��B
7. �A�v�������s����ƃt�@�C���u���E�U[^2]�������オ��̂ŁA4. �ō쐬���� PNG �t�@�C����I������Ɖ摜�����̕\������܂��B�t�@�C������ `.360.png` �ƂȂ��Ă���ꍇ�́A�S�V���摜�Ƃ��ď�������܂��B `Sample_Images` �t�H���_�ɂ������T���v���摜�������Ă��܂��B�Ȃ��A�t�@�C���u���E�U�̈ʒu�����������ꍇ�́A�E�R���g���[���[�̃��S�}�[�N�{�^���������Đ��ʂ̈ʒu���C�����Ă��������B
8. ������@�͉�ʂ̍����ɕ\������Ă��܂��i���� Hand Trigger �������Ɛ����̕\���^��\����؂�ւ����܂��B�j�B���R���g���[���[�� Start �{�^���������ƁA�t�@�C���u���E�U���N�����܂��B�܂��A���̍ہA�\�����̉摜�̈ʒu�Ȃǂ�����������܂��B

[^1]: Depth Pro �̐���f�v�X�̍ő�l�� 10,000m �ł��B���̃f�v�X���� 10,000 �{�����l�� uint32 �ɂ��āA8 �r�b�g�����g���G���f�B�A���� RGBA �ɕۑ����Ă��܂��̂ŁA�A���t�@�`�����l���̒l�� 5 �ȓ��Ɏ��܂�܂��B���̂��߉E���͂قړ����̉摜�ƂȂ��Ă��܂��B

[^2]: �t�@�C���u���E�U�ɂ́A**UnitySimpleFileBrowser** [[URL](https://github.com/yasirkula/UnitySimpleFileBrowser)] ���g�p���Ă��܂��B��҂� S?leyman Yasir KULA ���Ɋ��Ӑ\���グ�܂��B

### �A�v���̋�̓I�Ȏg�p���@
- **Start �{�^���i���R���g���[���[�j** �t�@�C���u���E�U�[���N�����܂��B
- **���X�e�B�b�N** �摜�̈ʒu��O�㖔�͍��E�ɓ������܂��B
- **���g���K�[ + �R���g���[���[** �摜�̈ʒu�𓮂����܂��B�}�E�X�̃h���b�O�̂悤�Ɏg�p���܂��B
- **���n���h�g���K�[** ��������̕\���^��\����؂�ւ��܂��B
- **X / Y �{�^��** �摜�̉��s���𒲐����܂��B���s���� Log ���[�h�̎��̂ݍ�p���܂��iLog ���[�h�Ŏg�p���鉺�̐����� b �𑝌����܂��B�j�B
```math
z' = a \times Log(1 + z^b)
```
- **�E�X�e�B�b�N** ���E�ŉ摜���g��E�k�����܂��B
- **�E�g���K�[ + �R���g���[���[�O��** �摜�̉��s���𒲐����܂��i��̐����� a �𑝌����܂��B�j�B
- **A �{�^��** ���s���� Log ���[�h�ɂ��܂��B
- **B �{�^��** ���s���� Linear ���[�h�ɂ��܂��B
- **�E�n���h�g���K�[ + �R���g���[���[���E** �ȗ����a��ύX���܂��B�i�E�F�Ȃ�����傫���A���F��蕽�ʂɋ߂��j�B

### Unity Editor �ւ̓ǂݍ��݁i�����Ńr���h����������������������̐����j
- �\�[�X�t�@�C���̓��e��C�ӂ̃t�H���_�ɓ���A**`Assets` �t�H���_�[�̒��� Unity-StarterSamples v71.0.0 [[URL](https://github.com/oculus-samples/Unity-StarterSamples/releases/tag/v71.0.0)] ��ǉ����܂�**�i`Unity-StarterSamples-71.0.0` �Ƃ����t�H���_�Ƃ��̒��g�� `Assets` �t�H���_�ɓ����Ă����ԁB�j�B
- Unity Hub �� "Add project from disk" ���j���[�ŃC���X�g�[�������t�H���_���w�肵�܂��B
- `Assets > Scenes` ����A `Sample Scene` �� Hierarchy �E�C���h�E�Ƀh���b�v���Ă��������BUnity �������ǉ����� Scene �͍폜���Ă��������B
- Unity Editor ���ł��A�v���͓��삵�܂��i���s�O�� Meta Quest Link �փS�[�O����ڑ����邱�Ƃ��K�v�ł��B�j�B

### Q&A
**Q: �ʏ�� RGBD �摜�Ƃ̈Ⴂ�͉��ł����B**

**A:** �ʏ�ARGBD �摜�́A���摜�̉E����255�K���̃f�v�X����ێ����܂��BDepth Pro �t���� `run.py` �ł́A0.1m ���� 250m �܂ł̃f�v�X���̋t���Ŋe�s�N�Z���̃f�v�X�𐳋K�����A0 ���� 255 �܂ł̃f�v�X�����蓖�ĂĂ��܂��B���̕��@�ł́A�߂��ɂ����ʑ̂̃f�v�X���̉𑜓x�͍����ł����A�Ⴆ�Βn�ʁA�ǁA���A���R�f�荞�񂾏����ȕ��̂Ȃǂ���O�ɂ���A���Ƀ��C���̔�ʑ̂�����悤�ȉ摜�̏ꍇ�A���C���̔�ʑ̂̃f�v�X���̊K�����Ⴍ�Ȃ�A���ʂ̏��Ȃ��o�͂ƂȂ��Ă��܂��܂��i���̐}���Q�Ɓj�B����A���̃c�[���̃X�N���v�g�ł́A���� float �̃f�v�X�����P���{���� `uint32` �ŕێ����Ă���A���C���̔�ʑ̂ɃY�[�����Ă��f�v�X�̏�񂪌��̐���ǂ���ێ�����邱�Ƃ��痧�̊������Ȃ��܂���B

![fig](https://github.com/user-attachments/assets/15175e2d-41d7-4a30-a5a5-6748065f1ff2)

---
The following is an automatic translation by ChatGPT and is a provisional translation.

### Overview
Using the included Python script, this application creates a PNG image file that incorporates depth information estimated by Depth Pro, which is provided by Apple. By loading that PNG image, you can view it in 3D on devices like Meta Quest 2 via a Meta Quest Link application.

- **v1.0.1**: I corrected the calculation error.

A separate installation of Depth Pro  [[URL](https://github.com/apple/ml-depth-pro)] is required to run the Python script necessary for creating PNG files for 3D images.

### [Download Files](https://github.com/amariichi/QuestLinkRGBDEViewer/releases/tag/v1.0.1)

### Outline of Setup and Usage
1. Install Depth Pro following the instructions on the official page.
2. Copy `depth-pro_rgbde.py` to the folder where Depth Pro was installed, and create an `input` folder there as well.
3. Place the images (jpg or png) you want to view in stereoscopic 3D (e.g., on Quest 2) into the `input` folder.
4. Run the script by typing `python depth-pro_rgbde.py` in the terminal. A PNG file whose left half is the original image and whose right half is almost transparent[^3] will be generated in the `output` folder. When you use the `--sphere` option, the filename becomes xxx_RGBDE **.360** .png.
5. Unzip the downloaded application executable (from the link above) into any folder of your choice.
6. Connect your headset (such as Meta Quest 2) to your PC via Meta Quest Link [[URL](https://www.meta.com/ja-jp/help/quest/pcvr/)].
7. When you run the application, a file browser[^4] will appear. Select the PNG file you created in step 4 to view the image in 3D. In addition, if a filename ends with `.360.png`, it will be treated as a full-sphere image. Several sample images are included in the `Sample_Images` folder. If the file browser window appears in an odd position, press the logo-mark button on the right controller to reset the forward-facing direction.
8. The operation instructions are shown at the bottom-left of the screen. Press the left controller�fs Start button to open the file browser. This also resets the position of the currently displayed image.

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
 - **Right Trigger + Controller** Moves the image�fs depth forward or backward (adjusts the parameter a in the formula above).
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

**A:** Typically, an RGBD image includes 8-bit (0?255) depth information on the right side of the original image. In the run.py script provided with Depth Pro, depth values from 0.1m to 250m are normalized by taking the reciprocal and assigning a range from 0 to 255. While this gives high-resolution depth data for close-up subjects, if there are objects like ground, walls, grass, or small items in the foreground, the main subject in the background will have a lower depth range, resulting in reduced 3D details (see the figure below). In contrast, this tool�fs script multiplies the original floating-point depth values by 10,000 and stores them in uint32. As a result, even when zooming in on the main subject, the originally estimated depth remains intact, preserving a strong sense of 3D depth.

![fig](https://github.com/user-attachments/assets/15175e2d-41d7-4a30-a5a5-6748065f1ff2)



