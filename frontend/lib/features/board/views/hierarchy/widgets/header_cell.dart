import 'package:flutter/material.dart';

class HeaderCell extends StatelessWidget {
  const HeaderCell({
    super.key,
    required this.label,
    required this.width,
    required this.style,
  });

  final String label;
  final double width;
  final TextStyle? style;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: width,
      child: Text(label, style: style, overflow: TextOverflow.ellipsis),
    );
  }
}
