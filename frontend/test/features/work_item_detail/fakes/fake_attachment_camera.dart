import 'package:weaver/features/work_item_detail/data/attachment_camera.dart';
import 'package:weaver/features/work_item_detail/data/photo_capture_exception.dart';
import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';

/// Stands in for the device camera: [photo] is what the user takes (null
/// cancels), [failure] is thrown instead when set, and each opening of the
/// camera is counted.
class FakeAttachmentCamera implements AttachmentCamera {
  FakeAttachmentCamera({this.isAvailable = false, this.photo, this.failure});

  @override
  final bool isAvailable;

  AttachmentUpload? photo;
  PhotoCaptureException? failure;
  int timesOpened = 0;

  @override
  Future<AttachmentUpload?> takePhoto() async {
    timesOpened++;
    if (failure case final error?) throw error;
    return photo;
  }
}
