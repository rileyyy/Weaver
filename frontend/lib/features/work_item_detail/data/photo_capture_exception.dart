/// The camera could not be used. [code] says why, using image_picker's
/// error codes: [accessDenied], [noCamera], or another for anything else.
class PhotoCaptureException implements Exception {
  const PhotoCaptureException(this.code, [this.message]);

  static const String accessDenied = 'camera_access_denied';
  static const String noCamera = 'no_available_camera';

  final String code;
  final String? message;

  @override
  String toString() => message ?? code;
}
