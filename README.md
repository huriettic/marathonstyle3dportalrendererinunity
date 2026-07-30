# Marathon-Style 3D Portal Renderer in Unity



This project implements a Marathon-style recursive 3D portal renderer in Unity, inspired by Bisqwit's portal rendering tutorial and Bunny83's VisPortals system.



The renderer can play Bisqwit's map-clear.txt tutorial level.



##### Note:



This repository does not contain any files from Bisqwit's portal rendering tutorial video.



[Watch the video on YouTube.](https://www.youtube.com/watch?v=HQYsFshbkYw)



Download map-clear.txt from the video description and place it in the project's Resources folder.



## 1\. Clip space geometric clipping



All triangles and portal polygons are clipped in clip space against the camera frustum.



This produces a clipped polygon fully inside the clip space frustum.



## 2\. Conversion to screen space and AABB generation



The clipped polygon is converted from clip space to screen space.



A screen space axis aligned bounding box (AABB) is made for triangles and portals.



## 3\. Screen space AABB intersection



Instead of converting NDC space AABBs back into clip space and re-clipping geometry, the renderer now does screen space AABB intersection.



This makes a reduced screen space area for rasterization.



## 4\. Stability guaranteed



Because triangles and portals are clipped in clip space first, all resulting AABBs are fully inside the frustum, it's a screen space AABB that is never invalid or an inverted rectangle and remains stable under deep portal recursion.



## 5\. Screen space discarding pixels



The fragment shader discards the original triangle pixels only outside the intersected screen space AABB.



This is significantly faster and simpler than geometric re-clipping with clip space.



#### Usage



Add map-clear.txt from Bisqwit's tutorial video to the Resources folder or use the included Two Hallways level.



Toggle debug mode to visualize portals.



[NDC space AABB portals in clip space video.](https://www.youtube.com/watch?v=zMMPdxAyXXU)



#### Credits



This project uses code derived from VisPortals by Bunny83.



VisPortals by Bunny83

* License: MIT
* Copyright: © 2016 Bunny83
* [GitHub Source](https://github.com/Bunny83/UnityWebExamples/tree/master/VisPortals)

