import 'package:flutter/material.dart';
import 'package:weaver/shared/markdown/markdown_text.dart';
import 'package:weaver/shared/widgets/field_label.dart';

/// The description: rendered Markdown to read and tick, or the raw text to
/// edit. Switching back to the rendered view keeps any edits in
/// [controller]; they're saved with the rest of the details form.
class DescriptionField extends StatefulWidget {
  const DescriptionField({
    required this.controller,
    required this.onToggleTask,
    super.key,
  });

  final TextEditingController controller;

  /// Null while ticking would be unsafe, which leaves the boxes read-only.
  final ValueChanged<int>? onToggleTask;

  @override
  State<DescriptionField> createState() => _DescriptionFieldState();
}

class _DescriptionFieldState extends State<DescriptionField> {
  bool _isEditing = false;

  void _setEditing(bool editing) => setState(() => _isEditing = editing);

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Expanded(child: FieldLabel('Description')),
            if (_isEditing)
              IconButton(
                icon: const Icon(Icons.visibility_outlined, size: 18),
                tooltip: 'Preview description',
                onPressed: () => _setEditing(false),
              )
            else
              IconButton(
                icon: const Icon(Icons.edit, size: 18),
                tooltip: 'Edit description',
                onPressed: () => _setEditing(true),
              ),
          ],
        ),
        if (_isEditing)
          TextField(
            controller: widget.controller,
            decoration: const InputDecoration(
              helperText: 'Markdown. "- [ ] item" makes a checklist.',
            ),
            minLines: 4,
            maxLines: 16,
          )
        else
          _rendered(context),
      ],
    );
  }

  Widget _rendered(BuildContext context) {
    final text = widget.controller.text;
    if (text.trim().isEmpty) {
      return InkWell(
        onTap: () => _setEditing(true),
        child: Text(
          'No description',
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
            color: Theme.of(context).colorScheme.onSurfaceVariant,
          ),
        ),
      );
    }
    return MarkdownText(text, onToggleTask: widget.onToggleTask);
  }
}
