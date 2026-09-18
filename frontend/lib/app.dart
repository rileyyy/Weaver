import 'package:flutter/material.dart';

import 'package:weaver/core/theme/app_theme.dart';
import 'package:weaver/features/auth/auth_gate.dart';

class WeaverApp extends StatelessWidget {
  const WeaverApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Weaver',
      theme: AppTheme.light,
      darkTheme: AppTheme.dark,
      // The debug banner's hit-test region sits in the same top-right
      // corner as AppBar actions (see BoardView's sign-out button), so it
      // can swallow taps meant for them in debug builds. It's stripped
      // automatically from profile/release builds either way.
      debugShowCheckedModeBanner: false,
      home: const AuthGate(),
    );
  }
}
