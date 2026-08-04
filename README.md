# Marathon-Style 3D Portal Renderer in Unity



This project implements a Marathon-style recursive 3D portal rendering system in Unity, inspired by Bisqwit's portal rendering tutorial and Bunny83's VisPortals system.

It supports Bisqwit's map-clear.txt tutorial level and includes an additional level, Two Hallways.



### Note:



This repository does not contain any files from Bisqwit's portal rendering tutorial video.

Download map-clear.txt from the video description and place it in the project's Resources folder.



## 1\. Clip space geometric clipping



All portal polygons are clipped in clip space against left, right, bottom, top, near and far planes in the clip space frustums.



## 2\. Conversion to NDC space \& AABB generation



The clipped portal is converted from clip space to NDC space.

A NDC space axis aligned bounding box (AABB) is computed for portal polygons.

The AABB represent the exact visible region of each portal on the screen.



## 3\. NDC space AABB overlap



It converts the NDC space AABBs back into clip space to clip portals then the renderer performs NDC space AABB overlap test.

This produces a reduced NDC space AABB for each portal.



# 4\. Guaranteed correct AABBs



Because all portals are clipped in clip space first, every resulting AABB is fully inside the frustum and is never inverted or degenerate and remains stable under deep portal recursion.



## 5\. Screen space pixel discarding



The fragment shader can discard pixels outside of multiple screen space AABBs.

Only fragments inside the active portal rectangles are rendered.



### Usage



Download the project as a zip file and load the scene in a Unity editor.

Download map-clear.txt from Bisqwit's tutorial video and place it in the project's Resources folder or use the included Two Hallways level.

Toggle Debug Mode to visualize portal rectangles.

Press Play and the renderer will automatically load the level, build geometry, and begin portal rendering.



### Videos



[Watch Bisqwit's portal rendering tutorial video](https://www.youtube.com/watch?v=HQYsFshbkYw)



[Watch NDC space AABB portals in clip space video.](https://www.youtube.com/watch?v=zMMPdxAyXXU)



### Third party code



This project uses code derived from VisPortals by Bunny83.



VisPortals by Bunny83  

License: MIT

Copyright: © 2016 Bunny83

[GitHub Source](https://github.com/Bunny83/UnityWebExamples/tree/master/VisPortals)

