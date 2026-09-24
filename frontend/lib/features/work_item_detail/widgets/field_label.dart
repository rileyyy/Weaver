import 'package:flutter/material.dart';

/// A field's label, styled identically everywhere in the detail view. Some
/// fields (`TextField`) could otherwise use their own floating
/// `InputDecoration.labelText` while others (dropdowns with no built-in
/// label, the tags/schedule sections) need a standalone label above the
/// field — using this for all of them, instead of mixing the two
/// approaches, is what keeps every label the same size/weight/color.
class FieldLabel extends StatelessWidget {
  const FieldLabel(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Text(
        text,
        style: Theme.of(context).textTheme.labelLarge?.copyWith(
          color: Theme.of(context).colorScheme.onSurfaceVariant,
        ),
      ),
    );
  }
}
