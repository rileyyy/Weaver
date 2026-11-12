import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';
import 'package:weaver/features/work_item_detail/models/photo_downscale.dart';

/// The device camera, for attaching a photo taken on the spot. Behind an
/// interface so view models can be tested without a camera.
abstract class AttachmentCamera {
  /// Whether this device has a camera the app can open: true on phones,
  /// false on web and desktop.
  bool get isAvailable;

  /// Opens the camera and returns the photo taken, shrunk as [downscale]
  /// says; null if the user cancels. Throws [PhotoCaptureException] when the
  /// camera can't be used.
  Future<AttachmentUpload?> takePhoto(PhotoDownscale downscale);
}
