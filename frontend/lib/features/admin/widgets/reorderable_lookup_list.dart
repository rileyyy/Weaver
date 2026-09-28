import 'package:flutter/material.dart';

/// A lookup list reordered by dragging each row's handle, with edit and
/// delete buttons. The handle starts a drag straight away on every
/// platform, where the default long-press would hide the feature on
/// desktop and clash with the row's own buttons.
class ReorderableLookupList<T> extends StatelessWidget {
  const ReorderableLookupList({
    super.key,
    required this.items,
    required this.idOf,
    required this.titleOf,
    required this.onReorder,
    required this.onEdit,
    required this.onDelete,
    this.subtitleOf,
    this.swatchOf,
    this.deleteDisabledReason,
  });

  final List<T> items;
  final String Function(T item) idOf;
  final String Function(T item) titleOf;
  final String? Function(T item)? subtitleOf;
  final Color? Function(T item)? swatchOf;
  final void Function(int oldIndex, int newIndex) onReorder;
  final ValueChanged<T> onEdit;
  final ValueChanged<T> onDelete;

  /// When set, deleting is disabled and this explains why.
  final String? deleteDisabledReason;

  @override
  Widget build(BuildContext context) {
    return ReorderableListView.builder(
      buildDefaultDragHandles: false,
      padding: const EdgeInsets.only(bottom: 16),
      itemCount: items.length,
      onReorderItem: onReorder,
      itemBuilder: (context, index) {
        final item = items[index];
        final title = titleOf(item);
        final subtitle = subtitleOf?.call(item);
        final swatch = swatchOf?.call(item);
        return ListTile(
          key: ValueKey(idOf(item)),
          leading: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              ReorderableDragStartListener(
                index: index,
                child: const Tooltip(
                  message: 'Drag to reorder',
                  child: MouseRegion(
                    cursor: SystemMouseCursors.grab,
                    child: Icon(Icons.drag_indicator),
                  ),
                ),
              ),
              if (swatch != null) ...[
                const SizedBox(width: 12),
                CircleAvatar(radius: 10, backgroundColor: swatch),
              ],
            ],
          ),
          title: Text(title, overflow: TextOverflow.ellipsis),
          subtitle: subtitle == null ? null : Text(subtitle),
          trailing: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              IconButton(
                icon: const Icon(Icons.edit_outlined),
                tooltip: 'Edit $title',
                onPressed: () => onEdit(item),
              ),
              IconButton(
                icon: const Icon(Icons.delete_outline),
                tooltip: deleteDisabledReason ?? 'Delete $title',
                onPressed: deleteDisabledReason == null
                    ? () => onDelete(item)
                    : null,
              ),
            ],
          ),
        );
      },
    );
  }
}
