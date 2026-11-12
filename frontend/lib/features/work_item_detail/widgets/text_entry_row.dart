import 'dart:async';

import 'package:flutter/material.dart';

/// A text field with a submit button, cleared once [onSubmit] reports the
/// entry was accepted. Shared by the tag, link and comment inputs.
class TextEntryRow extends StatefulWidget {
  const TextEntryRow({
    super.key,
    required this.hintText,
    required this.buttonLabel,
    required this.onSubmit,
    this.maxLines = 1,
    this.controller,
  });

  final String hintText;
  final String buttonLabel;
  final int maxLines;

  /// Called with the trimmed, non-empty text; returns whether it was
  /// accepted (and the field should clear).
  final Future<bool> Function(String text) onSubmit;

  /// For a parent that reacts to what's typed, such as mention suggestions.
  /// The parent then owns and disposes it; without one the row makes its own.
  final TextEditingController? controller;

  @override
  State<TextEntryRow> createState() => _TextEntryRowState();
}

class _TextEntryRowState extends State<TextEntryRow> {
  TextEditingController? _ownController;

  TextEditingController get _controller =>
      widget.controller ?? (_ownController ??= TextEditingController());

  @override
  void dispose() {
    _ownController?.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final text = _controller.text.trim();
    if (text.isEmpty) return;
    final accepted = await widget.onSubmit(text);
    if (accepted && mounted) _controller.clear();
  }

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: TextField(
            controller: _controller,
            decoration: InputDecoration(hintText: widget.hintText),
            maxLines: widget.maxLines,
            onSubmitted: widget.maxLines == 1
                ? (_) => unawaited(_submit())
                : null,
          ),
        ),
        const SizedBox(width: 8),
        TextButton(
          onPressed: () => unawaited(_submit()),
          child: Text(widget.buttonLabel),
        ),
      ],
    );
  }
}
