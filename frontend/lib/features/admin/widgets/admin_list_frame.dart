import 'package:flutter/material.dart';
import 'package:weaver/features/admin/widgets/admin_add_action.dart';
import 'package:weaver/shared/widgets/load_error_view.dart';

/// Every Admin tab's layout: the loading and error states, then an optional
/// [header] (such as a Space picker), a short explanation and an add button
/// above the list. A null [addAction] leaves the button out, for a tab that
/// only reports. Kept to a readable width on wide screens.
class AdminListFrame extends StatelessWidget {
  const AdminListFrame({
    super.key,
    required this.isLoading,
    required this.loadError,
    required this.onRetry,
    required this.description,
    required this.child,
    this.addAction,
    this.header,
  });

  final bool isLoading;
  final String? loadError;
  final Future<void> Function() onRetry;
  final String description;
  final AdminAddAction? addAction;
  final Widget child;
  final Widget? header;

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
            if (header case final header?)
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
                child: header,
              ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
              child: Wrap(
                spacing: 16,
                runSpacing: 8,
                alignment: WrapAlignment.spaceBetween,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Text(description),
                  if (addAction case final addAction?)
                    FilledButton.icon(
                      onPressed: addAction.onPressed,
                      icon: const Icon(Icons.add),
                      label: Text(addAction.label),
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
