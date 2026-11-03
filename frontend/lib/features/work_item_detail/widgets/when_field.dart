import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/models/quick_pick.dart';
import 'package:weaver/features/work_item_detail/widgets/when_resolved_badge.dart';
import 'package:weaver/shared/widgets/field_label.dart';

/// The free-text When, whether it's resolved, and the quick picks that set
/// dates. The phrase saves on its own (on submit or when the field loses
/// focus) rather than with "Save details", like the other metadata fields.
class WhenField extends StatefulWidget {
  const WhenField({
    super.key,
    required this.when,
    required this.resolved,
    required this.onSave,
    required this.onQuickPick,
  });

  final String? when;
  final bool resolved;

  /// Called with the field's text when it may have changed; returns
  /// whether it was saved.
  final Future<bool> Function(String text) onSave;
  final Future<bool> Function(QuickPick pick) onQuickPick;

  /// Mirrors `WorkItem.WhenMaxLength` on the backend.
  static const int maxLength = 200;

  @override
  State<WhenField> createState() => _WhenFieldState();
}

class _WhenFieldState extends State<WhenField> {
  late final TextEditingController _controller = TextEditingController(
    text: widget.when ?? '',
  );
  final FocusNode _focus = FocusNode();

  @override
  void initState() {
    super.initState();
    _focus.addListener(_onFocusChanged);
  }

  @override
  void didUpdateWidget(WhenField oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.when != oldWidget.when) _controller.text = widget.when ?? '';
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

  // Submitting also takes the focus away, so without this the same text
  // would be sent twice with the same version and the second would 409.
  String? _inFlight;

  Future<void> _save() async {
    final text = _controller.text.trim();
    if (text == (widget.when ?? '') || text == _inFlight) return;
    _inFlight = text;
    try {
      await widget.onSave(text);
    } finally {
      _inFlight = null;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const FieldLabel('When'),
            const Spacer(),
            if (widget.when != null)
              WhenResolvedBadge(resolved: widget.resolved),
          ],
        ),
        TextField(
          controller: _controller,
          focusNode: _focus,
          maxLength: WhenField.maxLength,
          decoration: const InputDecoration(
            hintText: 'e.g. before the Christmas market',
            counterText: '',
          ),
          onSubmitted: (_) => unawaited(_save()),
        ),
        const SizedBox(height: 8),
        Wrap(
          spacing: 6,
          runSpacing: 6,
          children: [
            for (final pick in QuickPick.values)
              ActionChip(
                label: Text(pick.label),
                visualDensity: VisualDensity.compact,
                onPressed: () => unawaited(widget.onQuickPick(pick)),
              ),
          ],
        ),
      ],
    );
  }
}
