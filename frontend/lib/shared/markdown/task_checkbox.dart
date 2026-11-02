import 'package:flutter/material.dart';
import 'package:weaver/shared/markdown/task_toggle_scope.dart';

/// One task-list box in rendered Markdown. Tickable while the enclosing
/// [TaskToggleScope] has a callback.
class TaskCheckbox extends StatelessWidget {
  const TaskCheckbox({required this.value, super.key});

  final bool value;

  @override
  Widget build(BuildContext context) {
    final canToggle = TaskToggleScope.of(context)?.onToggle != null;
    return Checkbox(
      value: value,
      visualDensity: VisualDensity.compact,
      materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
      onChanged: canToggle ? (_) => TaskToggleScope.toggle(context) : null,
    );
  }
}
