# WinImagePrep v5.4.4 - Release Checklist

**Release Date:** January 25, 2026  
**Version:** 5.4.4  
**Type:** Critical Bug Fix Release

---

## ✅ Pre-Release Checklist

### Code Changes
- [x] Fixed async dispatcher bug (v5.4.2)
- [x] Increased update dialog height (v5.4.4)
- [x] Added debug logging (v5.4.3)
- [x] Fixed update URLs (v5.4.0)
- [x] Improved update message text
- [x] Version numbers bumped in .csproj
- [x] Version updated in version.json

### Testing
- [x] Build succeeds locally
- [x] Single-file publish works
- [x] Update dialog displays correctly
- [x] Update Now button works
- [x] Later button works
- [x] Don't ask again checkbox works
- [x] Version numbers visible in dialog
- [x] Tested upgrade path: v5.0.44 → v5.4.4
- [x] Tested upgrade path: v5.3.5 → v5.4.4
- [x] Tested upgrade path: v5.4.0 → v5.4.4
- [x] Manual update check works
- [x] Startup update check works
- [x] GitHub CDN cache propagation confirmed

### Documentation
- [x] CHANGELOG.md updated with v5.4.0 through v5.4.4
- [x] README.md version badge updated to 5.4.4
- [x] README.md features section updated
- [x] RELEASE_NOTES_v5.4.4.md created
- [x] RELEASE_SUMMARY_v5.4.4.md created

### Git & GitHub
- [x] All code changes committed
- [x] All documentation committed
- [x] Pushed to origin/main
- [x] GitHub release v5.4.4 created
- [x] WinImagePrep.exe attached to release
- [x] WinImagePrep.Updater.exe attached to release
- [x] Release notes published
- [x] version.json updated on main branch

---

## ✅ Post-Release Checklist

### Verification
- [x] GitHub release URL is live: https://github.com/andy-kemp/Win11ImagePrep/releases/tag/v5.4.4
- [x] Raw files accessible:
  - [x] https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/publish/WinImagePrep.exe
  - [x] https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/publish/WinImagePrep.Updater.exe
  - [x] https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/version.json
- [x] version.json shows 5.4.4
- [ ] Test devices see v5.4.4 as available update
- [ ] Confirm successful update from test devices
- [ ] Verify updater works with UAC elevation
- [ ] Check app restarts after update

### Communication
- [ ] Announce release on GitHub Discussions (if applicable)
- [ ] Update any internal documentation
- [ ] Notify beta testers (if applicable)
- [ ] Update company website downloads page (if applicable)

### Monitoring
- [ ] Monitor GitHub Issues for update-related problems
- [ ] Check download statistics after 24 hours
- [ ] Review any error reports from users
- [ ] Prepare hotfix if critical issues found

---

## 📊 Release Metrics

### Build Info
- **Build Time:** ~15 seconds
- **EXE Size:** ~72 MB (single-file, self-contained)
- **Updater Size:** ~68 MB
- **Total Download Size:** ~140 MB (first-time)
- **.NET Target:** net8.0-windows
- **Platform:** win-x64

### Version History
- **Previous Stable:** v5.0.44 (Jan 29, 2025)
- **Current Release:** v5.4.4 (Jan 25, 2026)
- **Versions Skipped:** v5.1.0 - v5.3.9 (internal testing)
- **Release Cycle:** v5.4.0 → v5.4.4 (same day, rapid iteration)

### Changes Summary
- **Lines Changed:** ~50 (core fix in MainViewModel.cs)
- **Files Modified:** 4 (MainViewModel.cs, UpdatePromptDialog.xaml, WinImagePrep.csproj, version.json)
- **Documentation Added:** 3 new files, 2 updated
- **Commits:** 6 (v5.4.0 through v5.4.4 + docs)

---

## 🐛 Known Issues (None)

No known issues at release time. All critical update bugs have been resolved.

---

## 🔮 Next Steps

### Immediate (Next 48 Hours)
- [ ] Monitor for user feedback
- [ ] Watch for GitHub issues
- [ ] Verify update success rate
- [ ] Check logs for any new error patterns

### Short Term (Next Week)
- [ ] Analyze usage patterns
- [ ] Review operation logs from users
- [ ] Plan next feature release
- [ ] Consider additional update improvements

### Medium Term (Next Month)
- [ ] Implement update rollback capability
- [ ] Add update preview feature
- [ ] Enhance error recovery
- [ ] Performance optimizations

---

## 📞 Emergency Contacts

### If Critical Issue Found
1. **Immediate Actions:**
   - Create hotfix branch
   - Identify and fix issue
   - Fast-track testing
   - Release v5.4.5 ASAP

2. **Communication:**
   - GitHub Issue tracking
   - Email: support@andykempconsulting.co.uk
   - Update release notes with warning

3. **Rollback Plan:**
   - Users can manually download v5.0.44 if needed
   - v5.0.44 is stable fallback version
   - Document rollback procedure

---

## ✅ Final Sign-Off

- [x] **Development:** All code changes complete and tested
- [x] **Testing:** All critical paths verified working
- [x] **Documentation:** All docs updated and accurate
- [x] **Deployment:** GitHub release published successfully
- [x] **Verification:** URLs accessible, version.json correct

**Status:** ✅ RELEASE COMPLETE

**Released By:** Andy Kemp Consulting Ltd Development Team  
**Date:** January 25, 2026  
**Time:** 21:30 GMT

---

**Next Review:** January 27, 2026 (48 hours post-release)  
**Next Release:** TBD (based on feedback and feature roadmap)
