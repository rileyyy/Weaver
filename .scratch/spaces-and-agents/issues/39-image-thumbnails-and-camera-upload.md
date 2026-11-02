# 39: Image thumbnails and camera upload

**What to build:** Image attachments show as thumbnails in the detail dialog and open full size on tap. On a phone you can take a photo with the camera and attach it directly.

**Blocked by:** 38

**Status:** ready-for-agent

- [ ] The server generates and stores a thumbnail for each image upload, and serves it separately
- [ ] The detail dialog shows thumbnails for images and an icon for other files
- [ ] Phones offer "take photo" alongside "choose file"; Android permissions are declared
- [ ] Large photos are handled within the size limit (downscaled on the client, or rejected with a clear message; decide)
