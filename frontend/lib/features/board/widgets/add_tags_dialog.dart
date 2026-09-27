import 'package:flutter/material.dart';
import 'package:weaver/features/board/state/tag_additions.dart';

/// Opens the quick "add tags" dialog used from cards, Hierarchy rows and
/// Roadmap rows. Tags are collected (typed, or picked from [suggestions])
/// and returned together when the user confirms; null or empty means
/// nothing should change. Removing tags stays in the detail dialog.
Future<List<String>?> showAddTagsDialog(
  BuildContext context, {
  required List<String> currentTags,
  required List<String> suggestions,
}) => showDialog<List<String>>(
  context: context,
  builder: (_) =>
      _AddTagsDialog(currentTags: currentTags, suggestions: suggestions),
);

class _AddTagsDialog extends StatefulWidget {
  const _AddTagsDialog({required this.currentTags, required this.suggestions});

  final List<String> currentTags;
  final List<String> suggestions;

  @override
  State<_AddTagsDialog> createState() => _AddTagsDialogState();
}

class _AddTagsDialogState extends State<_AddTagsDialog> {
  static const int _maxSuggestionsShown = 12;

  final TextEditingController _controller = TextEditingController();
  final FocusNode _focusNode = FocusNode();
  List<String> _pending = const [];

  @override
  void initState() {
    super.initState();
    _controller.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _controller.dispose();
    _focusNode.dispose();
    super.dispose();
  }

  /// [_pending] plus [tag], unless the item or [_pending] already has it.
  void _addPending(String tag) {
    final withItemTags = withTagsAdded(
      [...widget.currentTags, ..._pending],
      [tag],
    );
    setState(() {
      _pending = withItemTags.sublist(widget.currentTags.length);
    });
  }

  void _submitTyped() {
    _addPending(_controller.text);
    _controller.clear();
    _focusNode.requestFocus();
  }

  void _confirm() {
    _addPending(_controller.text);
    Navigator.of(context).pop(_pending);
  }

  @override
  Widget build(BuildContext context) {
    final suggestions = tagSuggestions(
      widget.suggestions,
      excluded: [...widget.currentTags, ..._pending],
      query: _controller.text,
    ).take(_maxSuggestionsShown).toList();
    final canConfirm =
        _pending.isNotEmpty || _controller.text.trim().isNotEmpty;

    return AlertDialog(
      title: const Text('Add tags'),
      content: SizedBox(
        width: 360,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          spacing: 12,
          children: [
            TextField(
              controller: _controller,
              focusNode: _focusNode,
              autofocus: true,
              maxLength: tagMaxLength,
              decoration: const InputDecoration(
                hintText: 'Type a tag and press Enter',
                counterText: '',
              ),
              onSubmitted: (_) => _submitTyped(),
            ),
            if (_pending.isNotEmpty)
              Wrap(
                spacing: 6,
                runSpacing: 6,
                children: [
                  for (final tag in _pending)
                    InputChip(
                      label: Text(tag),
                      visualDensity: VisualDensity.compact,
                      onDeleted: () => setState(() {
                        _pending = [
                          for (final other in _pending)
                            if (other != tag) other,
                        ];
                      }),
                    ),
                ],
              ),
            if (suggestions.isNotEmpty) ...[
              Text(
                'Existing tags',
                style: Theme.of(context).textTheme.labelMedium,
              ),
              Wrap(
                spacing: 6,
                runSpacing: 6,
                children: [
                  for (final tag in suggestions)
                    ActionChip(
                      label: Text(tag),
                      visualDensity: VisualDensity.compact,
                      onPressed: () => _addPending(tag),
                    ),
                ],
              ),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: canConfirm ? _confirm : null,
          child: const Text('Add'),
        ),
      ],
    );
  }
}
