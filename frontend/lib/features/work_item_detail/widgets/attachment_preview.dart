import 'dart:typed_data';

import 'package:flutter/material.dart';

/// A small square standing for an attachment in the list: its thumbnail
/// when it has one, otherwise an icon for its kind of file.
class AttachmentPreview extends StatelessWidget {
  const AttachmentPreview({
    super.key,
    required this.contentType,
    required this.thumbnail,
  });

  static const double size = 40;

  final String contentType;

  /// Null when the server has no thumbnail for the file.
  final Future<Uint8List>? thumbnail;

  @override
  Widget build(BuildContext context) {
    final thumbnail = this.thumbnail;
    if (thumbnail == null) return _icon();
    return SizedBox.square(
      dimension: size,
      child: FutureBuilder<Uint8List>(
        future: thumbnail,
        builder: (context, snapshot) {
          if (snapshot.hasError) return _icon();
          final bytes = snapshot.data;
          if (bytes == null) return _loading;
          return ClipRRect(
            borderRadius: BorderRadius.circular(4),
            child: Image.memory(
              bytes,
              fit: BoxFit.cover,
              gaplessPlayback: true,
              errorBuilder: (_, _, _) => _icon(),
            ),
          );
        },
      ),
    );
  }

  Widget _icon() =>
      SizedBox.square(dimension: size, child: Icon(iconFor(contentType)));

  static IconData iconFor(String contentType) {
    if (contentType.startsWith('image/')) return Icons.image_outlined;
    if (contentType == 'application/pdf') return Icons.picture_as_pdf_outlined;
    return Icons.insert_drive_file_outlined;
  }

  static const Widget _loading = Center(
    child: SizedBox.square(
      dimension: 16,
      child: CircularProgressIndicator(strokeWidth: 2),
    ),
  );
}
