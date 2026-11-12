import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/models/mention_completion.dart';
import 'package:weaver/features/work_item_detail/widgets/text_entry_row.dart';

/// A [TextEntryRow] that suggests [handles] while an `@` mention is being
/// typed. Suggestions show as chips under the field rather than in an
/// overlay, so they stay clear of the phone keyboard.
class MentionTextEntry extends StatefulWidget {
  const MentionTextEntry({
    super.key,
    required this.handles,
    required this.hintText,
    required this.buttonLabel,
    required this.onSubmit,
    this.maxLines = 1,
  });

  final List<String> handles;
  final String hintText;
  final String buttonLabel;
  final int maxLines;
  final Future<bool> Function(String text) onSubmit;

  @override
  State<MentionTextEntry> createState() => _MentionTextEntryState();
}

class _MentionTextEntryState extends State<MentionTextEntry> {
  static const _maxSuggestions = 5;

  final _controller = TextEditingController();
  MentionCompletion? _completion;

  @override
  void initState() {
    super.initState();
    _controller.addListener(_updateCompletion);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _updateCompletion() {
    final completion = MentionCompletion.at(
      _controller.text,
      _controller.selection.baseOffset,
    );
    if (completion?.start == _completion?.start &&
        completion?.query == _completion?.query) {
      return;
    }
    setState(() => _completion = completion);
  }

  void _pick(String handle) {
    _controller.value = _completion!.apply(_controller.value, handle);
  }

  @override
  Widget build(BuildContext context) {
    final suggestions = (_completion?.suggest(widget.handles) ?? const [])
        .take(_maxSuggestions)
        .toList();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        TextEntryRow(
          controller: _controller,
          hintText: widget.hintText,
          buttonLabel: widget.buttonLabel,
          maxLines: widget.maxLines,
          onSubmit: widget.onSubmit,
        ),
        if (suggestions.isNotEmpty)
          Padding(
            padding: const EdgeInsets.only(top: 4),
            child: Wrap(
              spacing: 8,
              runSpacing: 4,
              children: [
                for (final handle in suggestions)
                  ActionChip(
                    avatar: const Icon(Icons.smart_toy_outlined, size: 18),
                    label: Text('@$handle'),
                    onPressed: () => _pick(handle),
                  ),
              ],
            ),
          ),
      ],
    );
  }
}
