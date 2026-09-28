import 'package:flutter/material.dart';
import 'package:weaver/shared/widgets/load_error_view.dart';

/// Every Admin tab's layout: the loading and error states, then a short
/// explanation and an add button above the list. Kept to a readable width
/// on wide screens.
class AdminListFrame extends StatelessWidget {
  const AdminListFrame({
    super.key,
    required this.isLoading,
    required this.loadError,
    required this.onRetry,
    required this.description,
    required this.addLabel,
    required this.onAdd,
    required this.child,
  });

  final bool isLoading;
  final String? loadError;
  final Future<void> Function() onRetry;
  final String description;
  final String addLabel;
  final VoidCallback onAdd;
  final Widget child;

  static const double maxWidth = 760;

  @override
  Widget build(BuildContext context) {
    if (isLoading) return const Center(child: CircularProgressIndicator());
    if (loadError case final error?) {
      return LoadErrorView(message: error, onRetry: onRetry);
    }
    return Align(
      alignment: Alignment.topCenter,
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: maxWidth),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
              child: Wrap(
                spacing: 16,
                runSpacing: 8,
                alignment: WrapAlignment.spaceBetween,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Text(description),
                  FilledButton.icon(
                    onPressed: onAdd,
                    icon: const Icon(Icons.add),
                    label: Text(addLabel),
                  ),
                ],
              ),
            ),
            Expanded(child: child),
          ],
        ),
      ),
    );
  }
}
