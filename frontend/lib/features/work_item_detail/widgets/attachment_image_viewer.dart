import 'dart:typed_data';

import 'package:flutter/material.dart';

/// An image attachment at full size, filling the screen, to pinch or
/// scroll-wheel zoom and pan.
class AttachmentImageViewer extends StatefulWidget {
  const AttachmentImageViewer({
    super.key,
    required this.fileName,
    required this.load,
  });

  final String fileName;

  /// Fetches the original image; called once, when the viewer opens.
  final Future<Uint8List> Function() load;

  static Future<void> show(
    BuildContext context, {
    required String fileName,
    required Future<Uint8List> Function() load,
  }) => showDialog<void>(
    context: context,
    builder: (_) => AttachmentImageViewer(fileName: fileName, load: load),
  );

  @override
  State<AttachmentImageViewer> createState() => _AttachmentImageViewerState();
}

class _AttachmentImageViewerState extends State<AttachmentImageViewer> {
  late final Future<Uint8List> _image = widget.load();

  @override
  Widget build(BuildContext context) => Dialog.fullscreen(
    child: Scaffold(
      appBar: AppBar(
        leading: const CloseButton(),
        title: Text(widget.fileName, overflow: TextOverflow.ellipsis),
      ),
      body: FutureBuilder<Uint8List>(
        future: _image,
        builder: (context, snapshot) {
          if (snapshot.hasError) return _error(context);
          final bytes = snapshot.data;
          if (bytes == null) {
            return const Center(child: CircularProgressIndicator());
          }
          return InteractiveViewer(
            maxScale: 8,
            child: Center(
              child: Image.memory(
                bytes,
                fit: BoxFit.contain,
                errorBuilder: (context, _, _) => _error(context),
              ),
            ),
          );
        },
      ),
    ),
  );

  Widget _error(BuildContext context) => Center(
    child: Text(
      'Could not load ${widget.fileName}.',
      style: TextStyle(color: Theme.of(context).colorScheme.error),
    ),
  );
}
