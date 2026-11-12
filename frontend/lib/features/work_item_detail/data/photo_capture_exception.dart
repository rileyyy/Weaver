/// The camera could not be used, e.g. the user refused it permission or the
/// device has none.
class PhotoCaptureException implements Exception {
  const PhotoCaptureException(this.message);

  final String message;

  @override
  String toString() => message;
}
