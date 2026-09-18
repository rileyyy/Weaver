import 'package:flutter/material.dart';

import 'package:weaver/core/theme/app_theme.dart';
import 'package:weaver/features/home/home_view.dart';

class WeaverApp extends StatelessWidget {
  const WeaverApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Weaver',
      theme: AppTheme.light,
      darkTheme: AppTheme.dark,
      home: const HomeView(),
    );
  }
}
