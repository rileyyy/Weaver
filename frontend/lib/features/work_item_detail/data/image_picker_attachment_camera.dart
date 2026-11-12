import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:image_picker/image_picker.dart';
import 'package:injectable/injectable.dart';
import 'package:weaver/core/platform/phone_camera.dart';
import 'package:weaver/features/work_item_detail/data/attachment_camera.dart';
import 'package:weaver/features/work_item_detail/data/photo_capture_exception.dart';
import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';
import 'package:weaver/features/work_item_detail/models/photo_downscale.dart';

@LazySingleton(as: AttachmentCamera)
class ImagePickerAttachmentCamera implements AttachmentCamera {
  final ImagePicker _picker = ImagePicker();

  @override
  bool get isAvailable =>
      hasPhoneCamera(isWeb: kIsWeb, platform: defaultTargetPlatform);

  @override
  Future<AttachmentUpload?> takePhoto(PhotoDownscale downscale) async {
    final XFile? photo;
    try {
      photo = await _picker.pickImage(
        source: ImageSource.camera,
        maxWidth: downscale.maxWidth.toDouble(),
        maxHeight: downscale.maxHeight.toDouble(),
        imageQuality: downscale.imageQuality,
      );
    } on PlatformException catch (e) {
      // E.g. camera_access_denied, or no_available_camera on a device
      // without one.
      throw PhotoCaptureException(e.message ?? e.code);
    }
    if (photo == null) return null;
    return AttachmentUpload.photo(
      photo.name,
      await photo.readAsBytes(),
      takenAt: DateTime.now(),
    );
  }
}
