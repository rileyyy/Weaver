import 'package:flutter/material.dart';

/// Asks for a View's name; null when cancelled. Mirrors the server's
/// limit, so an overlong name is caught before it's sent.
Future<String?> showViewNameDialog(
  BuildContext context, {
  required String title,
  required String confirmLabel,
  String initialName = '',
}) => showDialog<String>(
  context: context,
  builder: (_) => _ViewNameDialog(
    title: title,
    confirmLabel: confirmLabel,
    initialName: initialName,
  ),
);

class _ViewNameDialog extends StatefulWidget {
  const _ViewNameDialog({
    required this.title,
    required this.confirmLabel,
    required this.initialName,
  });

  static const int maxLength = 100;

  final String title;
  final String confirmLabel;
  final String initialName;

  @override
  State<_ViewNameDialog> createState() => _ViewNameDialogState();
}

class _ViewNameDialogState extends State<_ViewNameDialog> {
  late final TextEditingController _name = TextEditingController(
    text: widget.initialName,
  );

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  void _confirm() {
    final name = _name.text.trim();
    if (name.isEmpty) return;
    Navigator.of(context).pop(name);
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.title),
      content: TextField(
        controller: _name,
        autofocus: true,
        maxLength: _ViewNameDialog.maxLength,
        decoration: const InputDecoration(labelText: 'Name'),
        onChanged: (_) => setState(() {}),
        onSubmitted: (_) => _confirm(),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: _name.text.trim().isEmpty ? null : _confirm,
          child: Text(widget.confirmLabel),
        ),
      ],
    );
  }
}
