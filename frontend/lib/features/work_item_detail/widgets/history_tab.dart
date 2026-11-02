import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/widgets/history_entry_tile.dart';
import 'package:weaver/features/work_item_detail/work_item_history_view_model.dart';

/// The detail dialog's History tab: every change to the item, newest first,
/// with who made it (an agent badge and the actor label it sent) and when.
class HistoryTab extends StatelessWidget {
  const HistoryTab({super.key, required this.viewModel});

  final WorkItemHistoryViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: viewModel,
      builder: (context, _) {
        if (viewModel.isLoading) {
          return const Center(child: CircularProgressIndicator());
        }
        if (viewModel.loadError case final error?) {
          return Center(child: Text(error));
        }
        if (viewModel.entries.isEmpty) {
          return const Center(child: Text('No changes recorded yet.'));
        }
        return ListView.separated(
          padding: const EdgeInsets.all(16),
          itemCount: viewModel.entries.length,
          separatorBuilder: (_, _) => const Divider(height: 1),
          itemBuilder: (context, index) =>
              HistoryEntryTile(entry: viewModel.entries[index]),
        );
      },
    );
  }
}
