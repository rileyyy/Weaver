import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/admin/custom_field_admin_view_model.dart';
import 'package:weaver/features/admin/widgets/admin_list_frame.dart';
import 'package:weaver/features/admin/widgets/custom_field_dialog.dart';
import 'package:weaver/features/admin/widgets/reorderable_lookup_list.dart';
import 'package:weaver/features/admin/widgets/show_admin_error.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/widgets/confirm_action.dart';

/// Opens the custom fields of the Type [typeId] (named [typeName]).
Future<void> showTypeFieldsPage(
  BuildContext context, {
  required String typeId,
  required String typeName,
}) => Navigator.of(context).push(
  MaterialPageRoute<void>(
    builder: (_) => TypeFieldsPage(typeId: typeId, typeName: typeName),
  ),
);

/// One Type's custom fields: added, edited, dragged into order and removed
/// here. Only admins reach it, and agents can't change fields at all.
class TypeFieldsPage extends StatefulWidget {
  const TypeFieldsPage({
    super.key,
    required this.typeId,
    required this.typeName,
    this.viewModel,
  });

  final String typeId;
  final String typeName;

  /// Normally resolved from DI; tests pass one in.
  final CustomFieldAdminViewModel? viewModel;

  @override
  State<TypeFieldsPage> createState() => _TypeFieldsPageState();
}

class _TypeFieldsPageState extends State<TypeFieldsPage> {
  late final CustomFieldAdminViewModel _viewModel =
      widget.viewModel ??
      getIt<CustomFieldAdminViewModel>(param1: widget.typeId);

  @override
  void initState() {
    super.initState();
    unawaited(_viewModel.load());
  }

  @override
  void dispose() {
    _viewModel.dispose();
    super.dispose();
  }

  static String _subtitle(CustomField field) => switch (field) {
    CustomField(kind: final kind) when kind.isSelect =>
      '${kind.label} · ${field.options.map((o) => o.name).join(', ')}',
    CustomField(rollsUp: true) => '${field.kind.label} · rolls up',
    _ => field.kind.label,
  };

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('${widget.typeName} fields')),
      body: ListenableBuilder(
        listenable: _viewModel,
        builder: (context, _) => AdminListFrame(
          isLoading: _viewModel.isLoading,
          loadError: _viewModel.loadError,
          onRetry: _viewModel.load,
          description:
              'Every ${widget.typeName} item has these fields. Changing an '
              'item’s Type clears its values. Drag to reorder.',
          addLabel: 'Add field',
          onAdd: () => unawaited(_add(context)),
          child: ReorderableLookupList<CustomField>(
            items: _viewModel.items,
            idOf: (field) => field.id,
            titleOf: (field) => field.name,
            subtitleOf: _subtitle,
            onReorder: (oldIndex, newIndex) {
              final messenger = ScaffoldMessenger.of(context);
              unawaited(
                _viewModel
                    .move(oldIndex, newIndex)
                    .then((error) => showAdminError(messenger, error)),
              );
            },
            onEdit: (field) => unawaited(_edit(context, field)),
            onDelete: (field) => unawaited(_delete(context, field)),
          ),
        ),
      ),
    );
  }

  Future<bool> _add(BuildContext context) => showCustomFieldDialog(
    context,
    onSave: (name, kind, options, {required rollsUp}) => _viewModel.create(
      name,
      kind,
      [for (final o in options) o.name],
      rollsUp: rollsUp,
    ),
  );

  Future<bool> _edit(BuildContext context, CustomField field) =>
      showCustomFieldDialog(
        context,
        initial: field,
        onSave: (name, _, options, {required rollsUp}) =>
            _viewModel.update(field.id, name, options, rollsUp: rollsUp),
      );

  Future<void> _delete(BuildContext context, CustomField field) async {
    final messenger = ScaffoldMessenger.of(context);
    final confirmed = await confirmAction(
      context,
      title: 'Remove ${field.name}?',
      message:
          'Every ${widget.typeName} item loses its ${field.name} value. Each '
          'item’s history keeps the value it had.',
      confirmLabel: 'Remove',
      isDestructive: true,
    );
    if (!confirmed) return;
    showAdminError(messenger, await _viewModel.delete(field.id));
  }
}
