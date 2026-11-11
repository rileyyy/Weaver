import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/saved_view.dart';
import 'package:weaver/features/board/views/header/widgets/view_menu_action.dart';

/// The header's View picker: the open View's name, opening a menu of every
/// View (the default starred) and what can be done with the open one.
/// Overview can't be changed, renamed or deleted, so those are left out
/// while it's open.
class ViewMenu extends StatelessWidget {
  const ViewMenu({
    super.key,
    required this.views,
    required this.current,
    required this.defaultViewId,
    required this.onOpen,
    required this.onAction,
  });

  final List<SavedView> views;
  final SavedView? current;
  final String? defaultViewId;
  final ValueChanged<SavedView> onOpen;
  final ValueChanged<ViewMenuAction> onAction;

  @override
  Widget build(BuildContext context) {
    final current = this.current;
    return PopupMenuButton<Object>(
      tooltip: 'Views',
      onSelected: (choice) => switch (choice) {
        final SavedView view => onOpen(view),
        final ViewMenuAction action => onAction(action),
        _ => null,
      },
      itemBuilder: (_) => [
        for (final view in views)
          PopupMenuItem<Object>(
            value: view,
            child: ListTile(
              contentPadding: EdgeInsets.zero,
              leading: view.id == current?.id
                  ? const Icon(Icons.check)
                  : const SizedBox(width: 24),
              title: Text(view.name),
              trailing: view.id == defaultViewId
                  ? const Tooltip(
                      message: 'Your default View',
                      child: Icon(Icons.star, size: 18),
                    )
                  : null,
            ),
          ),
        const PopupMenuDivider(),
        _action(ViewMenuAction.saveAs, Icons.add, 'Save as new View…'),
        if (current != null && !current.builtIn) ...[
          _action(
            ViewMenuAction.saveChanges,
            Icons.save_outlined,
            'Save changes to "${current.name}"',
          ),
          _action(ViewMenuAction.rename, Icons.edit_outlined, 'Rename…'),
        ],
        if (current != null && current.id != defaultViewId)
          _action(
            ViewMenuAction.makeDefault,
            Icons.star_outline,
            'Make my default',
          ),
        if (current != null && !current.builtIn)
          _action(ViewMenuAction.delete, Icons.delete_outline, 'Delete'),
      ],
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 4),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.view_quilt_outlined, size: 20),
            const SizedBox(width: 4),
            Text(
              current?.name ?? 'Views',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const Icon(Icons.arrow_drop_down),
          ],
        ),
      ),
    );
  }

  static PopupMenuItem<Object> _action(
    ViewMenuAction action,
    IconData icon,
    String label,
  ) => PopupMenuItem<Object>(
    value: action,
    child: ListTile(
      contentPadding: EdgeInsets.zero,
      leading: Icon(icon),
      title: Text(label),
    ),
  );
}
