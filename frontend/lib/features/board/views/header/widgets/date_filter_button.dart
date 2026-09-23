import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/board/widgets/date_format.dart';

class DateFilterButton extends StatelessWidget {
  const DateFilterButton({
    super.key,
    required this.label,
    required this.value,
    required this.onPicked,
  });

  final String label;
  final DateTime? value;
  final ValueChanged<DateTime?> onPicked;

  @override
  Widget build(BuildContext context) {
    return TextButton(
      onPressed: () => unawaited(_pick(context)),
      child: Text(value == null ? label : formatDate(value!)),
    );
  }

  Future<void> _pick(BuildContext context) async {
    final picked = await showDatePicker(
      context: context,
      initialDate: value ?? DateTime.now(),
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
    );
    if (picked != null) onPicked(picked);
  }
}
