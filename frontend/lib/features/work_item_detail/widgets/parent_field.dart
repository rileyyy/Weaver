import 'package:flutter/material.dart';
import 'package:weaver/shared/models/work_item.dart';

/// Shows the item's parent (or "None") and opens a picker on tap.
class ParentField extends StatelessWidget {
  const ParentField({super.key, required this.parent, required this.onTap});

  final WorkItem? parent;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final parent = this.parent;
    return InkWell(
      onTap: onTap,
      child: InputDecorator(
        decoration: const InputDecoration(
          suffixIcon: Icon(Icons.arrow_drop_down),
        ),
        child: Text(
          parent == null ? 'None' : '#${parent.number} ${parent.title}',
          overflow: TextOverflow.ellipsis,
        ),
      ),
    );
  }
}
