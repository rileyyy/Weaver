# 39: Image thumbnails and camera upload

**What to build:** Image attachments show as thumbnails in the detail dialog and open full size on tap. On a phone you can take a photo with the camera and attach it directly.

**Blocked by:** 38

**Status:** done

- [x] The server generates and stores a thumbnail for each image upload, and serves it separately
  - A WebP of at most 320px a side, stored as `<id>.thumbnail` beside the file and served from `GET /api/work-item-attachments/{id}/thumbnail` (inline, `nosniff`, cached as immutable). Attachments carry `hasThumbnail`. Delete and purge remove it through the store's `DeleteAsync`.
  - Uploads from before this ticket aren't backfilled; they keep showing an icon.
- [x] The detail dialog shows thumbnails for images and an icon for other files
  - Tapping an image row opens it full size (pinch to zoom); other rows still download on tap, and image rows download from their own button.
- [x] Phones offer "take photo" alongside "choose file"; Android permissions are declared
  - Android: `CAMERA`, `uses-feature camera required=false` (so camera-less devices can still install), and a `<queries>` entry for `IMAGE_CAPTURE`. iOS: camera and photo library usage descriptions.
  - Not yet tried on a real device: the permission prompt and the downscale still need a check on a phone.
- [x] Large photos are handled within the size limit (downscaled on the client, or rejected with a clear message; decide)
  - Decided: both. Camera photos are downscaled on the device (2560px, quality 85), which keeps them far below the limit; a chosen file over the limit is refused by the server with its 413 message.

## Comments

**2026-11-12, from the implementation:**

- **Which images get thumbnails.** The server decides by decoding the content, never by the declared type, and only decodes JPEG, PNG, GIF, WebP and BMP. HEIC, TIFF, SVG and anything over 50 megapixels get an icon instead. A thumbnail that can't be made never fails the upload.
- **Thumbnails are made in the upload request.** Decoding a large image costs memory there (about 200 MB at the 50 MP cap); the README's deployment reference says so. Moving it to a background job is possible later if uploads get slow.
- **Thumbnails carry no metadata.** EXIF, XMP, IPTC and ICC are stripped, since a phone photo's EXIF can hold its location. Wide-gamut photos may look slightly less saturated in the thumbnail.
- **ImageSharp is held at 3.1.12.** From 3.2.0, Release builds fail without a Six Labors license key. 3.1.12's advisories are suppressed in `Directory.Packages.props`, each with the reason it can't be reached from our code. Getting a key and moving to 3.2.0 needs no code changes.
- **MCP shows `hasThumbnail`** on attachment listings (they share `AttachmentDto`), but there's no tool to fetch a thumbnail; agents use `get_work_item_attachment`.
- **Photo names.** Camera files are named `Photo YYYY-MM-DD HH.MM.SS.jpg` rather than the camera's random name.
