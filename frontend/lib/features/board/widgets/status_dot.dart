import 'package:flutter/material.dart';

/// A small colored circle indicating a status's configured color — falls
/// back to a neutral outline color if a status has none (e.g. a test
/// fixture that doesn't care about color).
class StatusDot extends StatelessWidget {
  const StatusDot({super.key, required this.color});

  final Color? color;

  static const double _size = 10;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: _size,
      height: _size,
      decoration: BoxDecoration(
        color: color ?? Theme.of(context).colorScheme.outlineVariant,
        shape: BoxShape.circle,
      ),
    );
  }
}
