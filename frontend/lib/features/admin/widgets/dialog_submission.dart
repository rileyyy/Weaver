import 'package:flutter/widgets.dart';

/// The save-and-close step every Admin dialog's state shares: runs
/// [submit], closes the dialog with true when it returns no error, and
/// otherwise keeps it open to show the error.
mixin DialogSubmission<T extends StatefulWidget> on State<T> {
  bool isSaving = false;
  String? error;

  Future<void> runSubmission(Future<String?> Function() submit) async {
    setState(() {
      isSaving = true;
      error = null;
    });
    final failure = await submit();
    if (!mounted) return;
    if (failure == null) {
      Navigator.of(context).pop(true);
      return;
    }
    setState(() {
      isSaving = false;
      error = failure;
    });
  }
}
