# Marathon-Style 3D Portal Renderer in Unity



This project implements a Marathon style 3D portal renderer in Unity, inspired by Bisqwit's portal rendering tutorial and Bunny83’s VisPortals system.



The renderer is capable of playing Bisqwit's map-clear.txt tutorial level.



##### Note:



This repository does not contain any files from Bisqwit's portal rendering tutorial video.

You must download map-clear.txt from the video description and place it in the project’s Resources folder.



[Watch the video on YouTube.](https://www.youtube.com/watch?v=HQYsFshbkYw)



#### 1\. Clip space geometric clipping



Portals and triangles are first clipped in clip space against:



the camera frustum



portal planes



portal rectangles



This ensures all surviving geometry lies fully inside the clip space frustum.



#### 2\. NDC space AABB generation



The clipped vertices are converted to NDC space, where an axis aligned bounding box (AABB) is computed.

This AABB represents the visible region of the portal.



#### 3\. Clipping using AABBs



When clipping portals against an AABB, the AABB is converted back into clip space so the same clipper can be used to clip triangles. The triangles are clipped by portal AABB and the triangles AABB is converted to screen space.



#### 4\. The guarantee



If a portal or triangle has already been clipped by the frustum, any AABB created from the clipped geometry is guaranteed to lie entirely inside the NDC frustum.



#### 5\. Screen space rasterization



The fragment shader rasterizes triangles only inside their screen space AABB.



##### Usage



Add map-clear.txt from Bisqwit's tutorial video to the Resources folder.



(the file is not included in this repository)



Or use the included Two Hallways level.



Toggle Debug Mode to visualize portal regions.



[NDC space AABB portals in clip space video.](https://www.youtube.com/watch?v=zMMPdxAyXXU)



##### Credits



This project uses code from VisPortals by Bunny83.



VisPortals by Bunny83  

* License: MIT
* Copyright: © 2016 Bunny83
* [GitHub Source](https://github.com/Bunny83/UnityWebExamples/tree/master/VisPortals)

