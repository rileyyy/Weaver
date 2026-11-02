import 'package:flutter/widgets.dart';
import 'package:weaver/shared/markdown/task_checkbox.dart';

/// Hands the current toggle callback to the [TaskCheckbox]es below it.
///
/// The Markdown widget builds its checkboxes once per parse and only
/// reparses when the text changes, so a callback captured at build time
/// would go stale; an inherited widget reaches them on every change. It
/// also numbers the boxes: the renderer builds a list item's box after its
/// nested items, so numbering by build order would be wrong for nested
/// lists, while the element tree keeps document order.
class TaskToggleScope extends InheritedWidget {
  const TaskToggleScope({
    required this.onToggle,
    required super.child,
    super.key,
  });

  /// Null when the boxes are read-only.
  final ValueChanged<int>? onToggle;

  static TaskToggleScope? of(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<TaskToggleScope>();

  /// Reports the tap on [checkbox] as its task index.
  static void toggle(BuildContext checkbox) {
    final scope = checkbox
        .getElementForInheritedWidgetOfExactType<TaskToggleScope>();
    final onToggle = (scope?.widget as TaskToggleScope?)?.onToggle;
    if (scope == null || onToggle == null) return;

    final boxes = <Element>[];
    void collect(Element element) {
      if (element.widget is TaskCheckbox) {
        boxes.add(element);
      } else {
        element.visitChildren(collect);
      }
    }

    scope.visitChildren(collect);
    final index = boxes.indexOf(checkbox as Element);
    if (index >= 0) onToggle(index);
  }

  @override
  bool updateShouldNotify(TaskToggleScope oldWidget) =>
      onToggle != oldWidget.onToggle;
}
