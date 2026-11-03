import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/lookup_admin_view_model.dart';
import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/models/lookup_value.dart';
import 'package:weaver/features/admin/space_admin_view_model.dart';
import 'package:weaver/features/admin/widgets/admin_list_frame.dart';
import 'package:weaver/features/admin/widgets/delete_lookup_dialog.dart';
import 'package:weaver/features/admin/widgets/name_dialog.dart';
import 'package:weaver/features/admin/widgets/reorderable_lookup_list.dart';
import 'package:weaver/features/admin/widgets/show_admin_error.dart';
import 'package:weaver/features/admin/widgets/space_picker.dart';
import 'package:weaver/features/admin/widgets/type_dialog.dart';
import 'package:weaver/shared/models/hex_color.dart';

/// The Priorities or Types tab. Priorities are shown one Space at a time,
/// chosen from [spaces], which priorities need and types ignore; types are
/// shared by every Space.
class LookupTab extends StatelessWidget {
  const LookupTab({super.key, required this.viewModel, this.spaces});

  final LookupAdminViewModel viewModel;
  final SpaceAdminViewModel? spaces;

  LookupKind get _kind => viewModel.kind;

  String get _description => switch (_kind) {
    LookupKind.priorities =>
      'This Space’s priorities, from least to most urgent. Drag to reorder.',
    LookupKind.types =>
      'Types label what kind of thing a work item is, and colour its card '
          'on the board. Drag to reorder.',
  };

  @override
  Widget build(BuildContext context) {
    final singular = _kind.singular;
    final spaces = _kind.ownedBySpace ? this.spaces : null;
    return ListenableBuilder(
      listenable: Listenable.merge([viewModel, ?spaces]),
      builder: (context, _) => AdminListFrame(
        header: spaces == null
            ? null
            : SpacePicker(
                spaces: spaces.spaces,
                selectedId: viewModel.spaceId,
                onSelected: (id) => unawaited(viewModel.showSpace(id)),
              ),
        isLoading: viewModel.isLoading,
        loadError: viewModel.loadError,
        onRetry: viewModel.load,
        description: _description,
        addLabel: 'Add $singular',
        onAdd: viewModel.hasList ? () => unawaited(_add(context)) : null,
        child: ReorderableLookupList<LookupValue>(
          items: viewModel.items,
          idOf: (value) => value.id,
          titleOf: (value) => value.name,
          onReorder: (oldIndex, newIndex) {
            final messenger = ScaffoldMessenger.of(context);
            unawaited(
              viewModel
                  .move(oldIndex, newIndex)
                  .then((error) => showAdminError(messenger, error)),
            );
          },
          swatchOf: _kind.hasColor
              ? (value) => parseHexColor(value.color)
              : null,
          onEdit: (value) => unawaited(_edit(context, value)),
          onDelete: (value) => unawaited(_delete(context, value)),
        ),
      ),
    );
  }

  Future<bool> _add(BuildContext context) {
    if (_kind.hasColor) {
      return showTypeDialog(
        context,
        onSave: (name, color) => viewModel.create(name, color: color),
      );
    }
    return showNameDialog(
      context,
      title: 'Add ${_kind.singular}',
      label: 'Name',
      submitLabel: 'Add',
      onSave: viewModel.create,
    );
  }

  Future<bool> _edit(BuildContext context, LookupValue value) {
    if (_kind.hasColor) {
      return showTypeDialog(
        context,
        initial: value,
        onSave: (name, color) => viewModel.update(value.id, name, color: color),
      );
    }
    return showNameDialog(
      context,
      title: 'Rename ${_kind.singular}',
      label: 'Name',
      submitLabel: 'Save',
      initialName: value.name,
      onSave: (name) => viewModel.update(value.id, name),
    );
  }

  Future<void> _delete(BuildContext context, LookupValue value) =>
      showDeleteLookupDialog(
        context,
        kind: _kind.singular,
        name: value.name,
        noneLabel: 'No ${_kind.singular}',
        replacements: [
          for (final other in viewModel.items)
            if (other.id != value.id) (id: other.id, name: other.name),
        ],
        onDelete: (replacementId) =>
            viewModel.delete(value.id, replacementId: replacementId),
      );
}
