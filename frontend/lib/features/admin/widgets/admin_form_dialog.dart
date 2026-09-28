import 'package:flutter/material.dart';

/// The frame every Admin dialog shares: its fields, the error from the last
/// attempt, and Cancel / a submit button that shows progress. The dialog's
/// own state owns the fields and the saving flag.
class AdminFormDialog extends StatelessWidget {
  const AdminFormDialog({
    super.key,
    required this.title,
    required this.content,
    required this.submitLabel,
    required this.onSubmit,
    required this.isSaving,
    this.error,
    this.isDestructive = false,
  });

  final String title;
  final Widget content;
  final String submitLabel;

  /// Null disables the submit button, e.g. while a required field is empty.
  final VoidCallback? onSubmit;
  final bool isSaving;
  final String? error;
  final bool isDestructive;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final error = this.error;
    return AlertDialog(
      title: Text(title),
      content: SizedBox(
        width: 380,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              content,
              if (error != null)
                Padding(
                  padding: const EdgeInsets.only(top: 12),
                  child: Text(error, style: TextStyle(color: colors.error)),
                ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: isSaving ? null : () => Navigator.of(context).pop(false),
          child: const Text('Cancel'),
        ),
        FilledButton(
          style: isDestructive
              ? FilledButton.styleFrom(backgroundColor: colors.error)
              : null,
          onPressed: isSaving ? null : onSubmit,
          child: Text(isSaving ? 'Saving…' : submitLabel),
        ),
      ],
    );
  }
}
