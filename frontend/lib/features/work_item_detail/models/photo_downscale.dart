/// How far the camera shrinks and recompresses a photo before it is
/// attached.
class PhotoDownscale {
  const PhotoDownscale({
    required this.maxWidth,
    required this.maxHeight,
    required this.imageQuality,
  });

  /// A full-resolution phone photo can pass the server's 25 MB limit; at
  /// 2560 px and JPEG quality 85 it is a few MB at most, still sharp enough
  /// to read a whiteboard or a label, so taking a photo never fails on size.
  static const PhotoDownscale forUpload = PhotoDownscale(
    maxWidth: 2560,
    maxHeight: 2560,
    imageQuality: 85,
  );

  final int maxWidth;
  final int maxHeight;

  /// JPEG quality, 0 to 100.
  final int imageQuality;
}
