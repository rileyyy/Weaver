import 'package:flutter/material.dart';

/// Shows [error] in a snackbar, or nothing for null (success). Takes the
/// messenger rather than a context so callers can look it up before the
/// await that produced the error.
void showAdminError(ScaffoldMessengerState messenger, String? error) {
  if (error == null) return;
  messenger.showSnackBar(SnackBar(content: Text(error)));
}
