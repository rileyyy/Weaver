import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';

/// A typed value for the text-like kinds (text, Markdown, number, currency,
/// URL), saved on submit or when the field loses focus, like the When
/// field. A blank entry clears the value. Obvious mistakes (a word in a
/// number) are caught here; the server has the final say.
class CustomFieldTextEditor extends StatefulWidget {
  const CustomFieldTextEditor({
    super.key,
    required this.kind,
    required this.value,
    required this.onSave,
  });

  final CustomFieldKind kind;
  final Object? value;

  /// Called with the value to store (null clears it); returns whether it
  /// was saved.
  final Future<bool> Function(Object? value) onSave;

  /// The text a stored [value] is edited as.
  static String textOf(CustomFieldKind kind, Object? value) => switch (value) {
    null => '',
    final num amount when kind == CustomFieldKind.currency =>
      amount.toStringAsFixed(2),
    final num number when number == number.roundToDouble() =>
      number.toInt().toString(),
    _ => '$value',
  };

  /// The value [text] stands for, or a reason it can't be saved.
  static ({Object? value, String? error}) parse(
    CustomFieldKind kind,
    String text,
  ) {
    final trimmed = text.trim();
    if (trimmed.isEmpty) return (value: null, error: null);
    switch (kind) {
      case CustomFieldKind.number || CustomFieldKind.currency:
        final number = num.tryParse(trimmed.replaceAll(',', '.'));
        if (number == null) return (value: null, error: 'Enter a number.');
        return (value: number, error: null);
      case CustomFieldKind.url:
        final uri = Uri.tryParse(trimmed);
        final isWeb =
            uri != null &&
            (uri.scheme == 'http' || uri.scheme == 'https') &&
            uri.host.isNotEmpty;
        return isWeb
            ? (value: trimmed, error: null)
            : (value: null, error: 'Enter an http or https address.');
      default:
        return (value: trimmed, error: null);
    }
  }

  @override
  State<CustomFieldTextEditor> createState() => _CustomFieldTextEditorState();
}

class _CustomFieldTextEditorState extends State<CustomFieldTextEditor> {
  late final TextEditingController _controller = TextEditingController(
    text: _savedText,
  );
  final FocusNode _focus = FocusNode();
  String? _error;

  // A blur right after a submit would otherwise send the same value twice,
  // and the second would be based on a stale version.
  Future<void>? _inFlight;

  String get _savedText =>
      CustomFieldTextEditor.textOf(widget.kind, widget.value);

  bool get _isNumeric =>
      widget.kind == CustomFieldKind.number ||
      widget.kind == CustomFieldKind.currency;

  @override
  void initState() {
    super.initState();
    _focus.addListener(_onFocusChanged);
  }

  @override
  void didUpdateWidget(CustomFieldTextEditor oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.value != oldWidget.value) _controller.text = _savedText;
  }

  @override
  void dispose() {
    _focus
      ..removeListener(_onFocusChanged)
      ..dispose();
    _controller.dispose();
    super.dispose();
  }

  void _onFocusChanged() {
    if (!_focus.hasFocus) unawaited(_save());
  }

  Future<void> _save() {
    if (_inFlight case final pending?) return pending;
    if (_controller.text.trim() == _savedText) {
      setState(() => _error = null);
      return Future.value();
    }
    final parsed = CustomFieldTextEditor.parse(widget.kind, _controller.text);
    setState(() => _error = parsed.error);
    if (parsed.error != null) return Future.value();

    final save = widget.onSave(parsed.value).then((_) {});
    _inFlight = save;
    return save.whenComplete(() => _inFlight = null);
  }

  @override
  Widget build(BuildContext context) {
    final isMarkdown = widget.kind == CustomFieldKind.markdown;
    return TextField(
      controller: _controller,
      focusNode: _focus,
      minLines: isMarkdown ? 3 : 1,
      maxLines: isMarkdown ? 8 : 1,
      keyboardType: switch (widget.kind) {
        CustomFieldKind.number || CustomFieldKind.currency =>
          const TextInputType.numberWithOptions(decimal: true, signed: true),
        CustomFieldKind.url => TextInputType.url,
        CustomFieldKind.markdown => TextInputType.multiline,
        _ => TextInputType.text,
      },
      textAlign: _isNumeric ? TextAlign.end : TextAlign.start,
      decoration: InputDecoration(
        hintText: isMarkdown ? 'Markdown' : null,
        errorText: _error,
      ),
      onSubmitted: isMarkdown ? null : (_) => unawaited(_save()),
    );
  }
}
