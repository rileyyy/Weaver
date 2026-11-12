import 'package:flutter/material.dart';
import 'package:weaver/shared/models/handle.dart';

/// The handle input both the add-agent and the handle dialogs show.
class HandleField extends StatelessWidget {
  const HandleField({
    super.key,
    required this.controller,
    required this.label,
    required this.enabled,
    this.autofocus = false,
    this.onChanged,
    this.onSubmitted,
  });

  final TextEditingController controller;
  final String label;
  final bool enabled;
  final bool autofocus;
  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onSubmitted;

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: controller,
      autofocus: autofocus,
      enabled: enabled,
      maxLength: maxHandleLength,
      autocorrect: false,
      enableSuggestions: false,
      decoration: InputDecoration(
        labelText: label,
        prefixText: '@',
        helperText:
            'Comments mention it as @handle, e.g. @claude-research. '
            'Leave blank so it can’t be mentioned.',
        helperMaxLines: 3,
      ),
      onChanged: onChanged,
      onSubmitted: onSubmitted,
    );
  }
}
