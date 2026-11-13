import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:image_picker/image_picker.dart';
import 'package:injectable/injectable.dart';
import 'package:weaver/core/platform/touch_platform.dart';
import 'package:weaver/features/work_item_detail/data/attachment_camera.dart';
import 'package:weaver/features/work_item_detail/data/photo_capture_exception.dart';
import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';

@LazySingleton(as: AttachmentCamera)
class ImagePickerAttachmentCamera implements AttachmentCamera {
  // A full-resolution phone photo can pass the server's 25 MB limit; at
  // 2560 px and JPEG quality 85 it is a few MB at most, still sharp enough
  // to read a whiteboard or a label, so taking a photo never fails on size.
  static const double _maxDimension = 2560;
  static const int _jpegQuality = 85;

  final ImagePicker _picker = ImagePicker();

  // A phone's browser can't open the camera through image_picker, and
  // desktops have no system camera UI, so only native phone apps offer it.
  @override
  bool get isAvailable => !kIsWeb && isTouchPlatform;

  @override
  Future<AttachmentUpload?> takePhoto() async {
    final XFile? photo;
    try {
      photo = await _picker.pickImage(
        source: ImageSource.camera,
        maxWidth: _maxDimension,
        maxHeight: _maxDimension,
        imageQuality: _jpegQuality,
      );
    } on PlatformException catch (e) {
      throw PhotoCaptureException(e.code, e.message);
    }
    if (photo == null) return null;
    return AttachmentUpload.photo(
      photo.name,
      await photo.readAsBytes(),
      takenAt: DateTime.now(),
    );
  }
}
