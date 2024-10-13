# Depth Scanner - Meta Quest 3 LiDAR Scanner

##概要
この Unity プロジェクトは、Meta Quest 3 VR ヘッドセットに搭載されているLiDARセンサーを利用して環境データをスキャンするアプリケーションです。
リアルタイムで周囲の環境を取得し、PLY形式の点群データとして出力することができます。
動作環境:Unity 2022.3.43f1
使用ライブラリ:
・Meta Depth API
・Meta Building Blocks
・Mixed-Reality SDK
・unity-google-drive
https://github.com/elringus/unity-google-drive?tab=readme-ov-file

加えて、以下のリポジトリを参考にしています。
・Meta_DepthAPI_Mesh_Generation
https://github.com/shanerob1106/Meta_DepthAPI_Mesh_Generation
・Marching-Cubes
https://github.com/SebLague/Marching-Cubes
・PointCloudShader
https://github.com/Kuwamai/PointCloudShader

##仕様
 - Chunk Loader<br>
  取得した点群は一定区間ごとの"Chunk"に振り分けられて保存され、描画の軽量化に利用されます。<br>
  Chunkごとに点数上限が設けられており、密度が高くなりすぎることを防止することで点群全体で密度を均一化する働きがあります。
 - Depth Scanner Shader<br>
   LiDARからの深度情報を元に周囲環境の形状を視覚化するShaderです。

##利用方法
 - 点群データの保存<br>
  コントローラの'X'ボタンを押すことで、その時点までにスキャンしていた点群データの保存処理が走ります。PLY形式で保存されます。<br>
  Press the 'X' button to save point cloud data

## 利用規約
 - MIT Licence

