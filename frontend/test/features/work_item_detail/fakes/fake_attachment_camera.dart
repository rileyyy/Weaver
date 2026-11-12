import 'package:weaver/features/work_item_detail/data/attachment_camera.dart';
import 'package:weaver/features/work_item_detail/data/photo_capture_exception.dart';
import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';
import 'package:weaver/features/work_item_detail/models/photo_downscale.dart';

/// Stands in for the device camera: [photo] is what the user takes (null
/// cancels), [failure] is thrown instead when set, and every request's
/// downscale options are recorded.
class FakeAttachmentCamera implements AttachmentCamera {
  FakeAttachmentCamera({this.isAvailable = false, this.photo, this.failure});

  @override
  final bool isAvailable;

  AttachmentUpload? photo;
  PhotoCaptureException? failure;
  final List<PhotoDownscale> requests = [];

  @override
  Future<AttachmentUpload?> takePhoto(PhotoDownscale downscale) async {
    requests.add(downscale);
    if (failure case final error?) throw error;
    return photo;
  }
}
