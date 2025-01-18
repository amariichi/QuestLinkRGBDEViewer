import torch
import cv2
import numpy as np
import matplotlib.pyplot as plt
import struct
from PIL import Image
import depth_pro
import os
import argparse

DEFAULT_FOLDER_PATH = './input'
ALLOWED_EXTENSIONS = ('.jpg', '.png')

def generate_depth_map(input_path, output_path):
    # Use GPU, if possible, GPUが利用可能な場合はGPUを使用
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print(f"Using device: {device}")
    
    # Load model and transform, モデルとトランスフォームの読み込み
    model, transform = depth_pro.create_model_and_transforms()
    model = model.to(device)
    model.eval()

    # Get file list of specified input folder, 指定した入力フォルダのファイルリストを取得
    files = [f for f in os.listdir(input_path) if os.path.isfile(os.path.join(input_path, f)) 
             and f.lower().endswith(ALLOWED_EXTENSIONS)]
    
    # Process files, ファイルを順番に処理
    for file in files:
        file_path = os.path.join(input_path, file)


        # Image loading and preprocessing, 画像の読み込みと前処理
        image, _, f_px = depth_pro.load_rgb(file_path)
        image = transform(image).unsqueeze(0).to(device)
    
        # Performing inference, 推論の実行
        with torch.no_grad():
            prediction = model.infer(image, f_px=f_px)
    
        # get depth map, デプスマップの取得
            depth = prediction["depth"].squeeze().cpu().numpy()
            #color_depth = depth.astype(np.uint8)

        # Convert the depth to uint32 and multiply by 10,000, デプスをuint32に変換し１万倍する。
        int_values = np.round(depth * 10000).astype(np.uint32)

        # Convert each integer to a 4-byte byte string and create a 3-dimensional byte array (RGBA each stored in 1 byte)
        # 各整数を4バイトのバイト列に変換し、3次元のバイト配列を作成（RGBAそれぞれ１バイトに格納する）
        byte_arrays = int_values.view(np.uint8).reshape(int_values.shape + (4,))

        # For little endian, check if byte order needs to be changed
        # リトルエンディアンの場合、バイトオーダーを変更する必要があるか確認
        if int_values.dtype.byteorder == '>' or (int_values.dtype.byteorder == '=' and np.little_endian == False):
            byte_arrays = byte_arrays[..., ::-1]

        # Save depth information as an image on the right side, デプスマップを右側に画像として保存
        depth_image = cv2.hconcat([cv2.cvtColor(cv2.imread(file_path), cv2.COLOR_BGR2BGRA), cv2.cvtColor(byte_arrays, cv2.COLOR_RGBA2BGRA)])
        cv2.imwrite(os.path.join(output_path, os.path.splitext(file)[0] + "_RGBDE.png"), depth_image, [int(cv2.IMWRITE_PNG_COMPRESSION), 9])
        print(f"Depth map of {file} is saved to {output_path}")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description='add depth information to all JPG or PNG images in the input folder and write them to the "output" folder')
    parser.add_argument('--folder', type=str, default=DEFAULT_FOLDER_PATH,
                        help='specify the folder to process(default: %(default)s)')

    args = parser.parse_args()
    input_image_path = args.folder  # Set imput image folder path, 入力画像のパスを指定
    output_image_path = "./output"  # Set output image folder path,出力画像のパスを指定
    generate_depth_map(input_image_path, output_image_path)
