#!/usr/bin/env python3
"""Failure-path tests for the macOS-compatible deployment transaction."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().with_name("deploy.sh")
REPO = "ghcr.io/pawelkam/pennypath"
OLD = REPO + "@sha256:" + "a" * 64
NEW = REPO + "@sha256:" + "b" * 64
PREVIOUS = REPO + "@sha256:" + "c" * 64

FAKE_DOCKER = r'''#!/usr/bin/env python3
import json, os, sys
from pathlib import Path
p = Path(os.environ["FAKE_STATE"])
s = json.loads(p.read_text())
a = sys.argv[1:]
s["calls"].append(a)
result, output = 0, ""
image = os.environ.get("APP_IMAGE", "")
if a[0] == "compose":
    a = a[a.index("compose.yaml") + 1:]
    if a[0] == "ps":
        output = "db" if a[-1] == "postgres" and s.get("db_healthy", True) else ""
        if a[-1] == "app":
            ids = (["app"] if s.get("image") else []) + s.get("extra_apps", [])
            if "--all" in a: ids += s.get("oneoffs", [])
            output = "\n".join(ids)
    elif a[0] == "up":
        assert a[-1] == "app" and "--no-deps" in a and "--pull" in a
        s["image"] = image
        if image == s.get("up_fail"): result = 1
    elif a[0] == "run":
        assert a == ["run", "--rm", "--no-deps", "--pull", "never", "app", "--migrate"]
        if image == s.get("migrate_fail"): result = 1
    elif a[0] == "port": output = "0.0.0.0:8080"
    elif a[0] == "stop": s["image"] = ""
elif a[0] == "ps":
    containers = []
    def add_container(identifier, service, oneoff="False", project=None):
        containers.append((identifier, {
            "com.docker.compose.project": project or os.environ.get("COMPOSE_PROJECT_NAME", "pennypath"),
            "com.docker.compose.service": service,
            "com.docker.compose.oneoff": oneoff,
        }))
    if s.get("image"): add_container("app", "app")
    if s.get("db_healthy", True): add_container("db", "postgres")
    for identifier in s.get("extra_apps", []): add_container(identifier, "app")
    for identifier in s.get("oneoffs", []): add_container(identifier, "app", "True")
    add_container("other-project-app", "app", project="unrelated-project")
    filters = [a[i + 1].removeprefix("label=").split("=", 1) for i, value in enumerate(a) if value == "--filter"]
    output = "\n".join(identifier for identifier, labels in containers
                       if all(labels.get(key) == value for key, value in filters))
elif a[0] == "pull":
    if a[-1] == s.get("pull_fail"): result = 1
elif a[0] == "inspect":
    if a[-1] not in ("app", "db"):
        result = 1
        print("error: no such object: " + a[-1], file=sys.stderr)
    elif "Config.Image" in a[2]: output = s.get("image", "")
    elif a[-1] == "db": output = "healthy"
    else: output = "unhealthy" if s.get("image") == s.get("unhealthy") else "healthy"
elif a[:2] == ["image", "inspect"]:
    if s.get("missing_cache"): result = 1
else: raise AssertionError(a)
p.write_text(json.dumps(s))
if output: print(output)
sys.exit(result)
'''


class DeploymentTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="pennypath-deploy-", dir="/private/tmp" if Path("/private/tmp").exists() else "/tmp")
        self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name)
        (self.directory / "compose.yaml").write_text("# mocked compose\n")
        (self.directory / ".env").write_text("# mocked server config\n")
        bin_dir = self.directory / "bin"
        bin_dir.mkdir()
        for name, content in {"docker": FAKE_DOCKER, "curl": "#!/bin/sh\nprintf 200\n", "sleep": "#!/bin/sh\nexit 0\n"}.items():
            path = bin_dir / name
            path.write_text(content)
            path.chmod(0o700)
        self.state = self.directory / "fake.json"
        self.state.write_text(json.dumps({"image": OLD, "calls": []}))
        self.record(OLD, PREVIOUS)
        self.env = {**os.environ, "PATH": str(bin_dir) + os.pathsep + os.environ["PATH"],
                    "DEPLOY_DIR": str(self.directory), "DEPLOY_HEALTH_TIMEOUT": "1", "FAKE_STATE": str(self.state)}

    def record(self, current, previous=""):
        (self.directory / "release.env").write_text(f"APP_IMAGE={current}\nPREVIOUS_IMAGE={previous}\n")

    def configure(self, **values):
        data = json.loads(self.state.read_text())
        data.update(values)
        self.state.write_text(json.dumps(data))

    def run_deploy(self, image=NEW, success=True):
        result = subprocess.run(["bash", str(SCRIPT), image], env=self.env, text=True, capture_output=True, timeout=15)
        self.assertEqual(result.returncode == 0, success, result.stdout + result.stderr)
        data = json.loads(self.state.read_text())
        for call in data["calls"]:
            if "up" in call or "stop" in call:
                self.assertEqual(call[-1], "app")
        self.assertFalse((self.directory / ".deploy-lock").exists())
        return data

    def test_success_records_previous(self):
        data = self.run_deploy()
        self.assertEqual(data["image"], NEW)
        calls = data["calls"]
        pull = next(i for i, call in enumerate(calls) if call[0] == "pull")
        migration = next(i for i, call in enumerate(calls) if "--migrate" in call)
        replacement = next(i for i, call in enumerate(calls) if "up" in call)
        self.assertLess(pull, migration)
        self.assertLess(migration, replacement)
        self.assertEqual((self.directory / "release.env").read_text(), f"APP_IMAGE={NEW}\nPREVIOUS_IMAGE={OLD}\n")

    def test_leftover_oneoff_containers_do_not_block_deployment(self):
        self.configure(oneoffs=["unhealthy-running-migrator", "stopped-migrator", "created-migrator"])
        self.env["COMPOSE_PROJECT_NAME"] = "custom-pennypath"
        data = self.run_deploy()
        self.assertEqual(data["image"], NEW)
        self.assertEqual({call[-1] for call in data["calls"] if call[0] == "inspect"}, {"app", "db"})

    def test_first_deployment_ignores_leftover_oneoff_without_app(self):
        (self.directory / "release.env").unlink()
        self.configure(image="", oneoffs=["stopped-migrator"])
        self.assertEqual(self.run_deploy()["image"], NEW)

    def test_multiple_service_containers_are_rejected_before_pull(self):
        before = (self.directory / "release.env").read_bytes()
        self.configure(extra_apps=["duplicate-app"])
        data = self.run_deploy(success=False)
        self.assertFalse(any(call[0] == "pull" or "up" in call for call in data["calls"]))
        self.assertFalse(any(call[0] == "inspect" for call in data["calls"]))
        self.assertEqual((self.directory / "release.env").read_bytes(), before)

    def test_pull_failure_keeps_running_app_and_state(self):
        self.configure(pull_fail=NEW)
        data = self.run_deploy(success=False)
        self.assertEqual(data["image"], OLD)
        self.assertFalse(any("up" in call for call in data["calls"]))
        self.assertIn(f"PREVIOUS_IMAGE={PREVIOUS}", (self.directory / "release.env").read_text())

    def test_unhealthy_release_restores_current(self):
        self.configure(unhealthy=NEW)
        self.assertEqual(self.run_deploy(success=False)["image"], OLD)
        self.assertIn(f"APP_IMAGE={OLD}", (self.directory / "release.env").read_text())

    def test_partial_compose_failure_restores_current(self):
        self.configure(up_fail=NEW)
        self.assertEqual(self.run_deploy(success=False)["image"], OLD)

    def test_manual_rollback_swaps_releases(self):
        data = self.run_deploy("rollback")
        self.assertEqual(data["image"], PREVIOUS)
        self.assertFalse(any("--migrate" in call for call in data["calls"]))
        self.assertEqual((self.directory / "release.env").read_text(), f"APP_IMAGE={PREVIOUS}\nPREVIOUS_IMAGE={OLD}\n")

    def test_retry_same_release_keeps_previous(self):
        self.run_deploy(OLD)
        self.assertIn(f"PREVIOUS_IMAGE={PREVIOUS}", (self.directory / "release.env").read_text())

    def test_initial_failure_stops_only_app(self):
        (self.directory / "release.env").unlink()
        self.configure(image="", unhealthy=NEW)
        self.assertEqual(self.run_deploy(success=False)["image"], "")
        self.assertFalse((self.directory / "release.env").exists())

    def test_initial_success_has_no_previous(self):
        (self.directory / "release.env").unlink()
        self.configure(image="")
        self.run_deploy()
        self.assertEqual((self.directory / "release.env").read_text(), f"APP_IMAGE={NEW}\nPREVIOUS_IMAGE=\n")

    def test_migration_failure_keeps_running_app_and_release_unchanged(self):
        before = (self.directory / "release.env").read_bytes()
        self.configure(migrate_fail=NEW)
        data = self.run_deploy(success=False)
        self.assertTrue(any("--migrate" in call for call in data["calls"]))
        self.assertEqual(data["image"], OLD)
        self.assertFalse(any("up" in call or "stop" in call for call in data["calls"]))
        self.assertEqual((self.directory / "release.env").read_bytes(), before)

    def test_initial_migration_failure_does_not_start_app_or_record_release(self):
        (self.directory / "release.env").unlink()
        self.configure(image="", migrate_fail=NEW)
        data = self.run_deploy(success=False)
        self.assertEqual(data["image"], "")
        self.assertFalse(any("up" in call or "stop" in call for call in data["calls"]))
        self.assertFalse((self.directory / "release.env").exists())

    def test_untracked_app_is_rejected(self):
        self.configure(image=PREVIOUS)
        self.assertEqual(self.run_deploy(success=False)["image"], PREVIOUS)

    def test_invalid_digest_is_rejected(self):
        self.assertEqual(self.run_deploy(REPO + ":latest", success=False)["image"], OLD)

    def test_missing_database_is_rejected(self):
        self.configure(db_healthy=False)
        self.assertEqual(self.run_deploy(success=False)["image"], OLD)

    def test_missing_rollback_image_is_rejected_before_change(self):
        self.configure(missing_cache=True)
        self.assertEqual(self.run_deploy(success=False)["image"], OLD)

    def test_no_previous_release(self):
        self.record(OLD)
        self.run_deploy("rollback", success=False)

    def test_rollback_failure_is_reported(self):
        self.configure(unhealthy=NEW, up_fail=OLD)
        self.run_deploy(success=False)
        self.assertIn(f"APP_IMAGE={OLD}", (self.directory / "release.env").read_text())

    def test_lock_rejects_concurrent_deployment(self):
        (self.directory / ".deploy-lock").mkdir()
        result = subprocess.run(["bash", str(SCRIPT), NEW], env=self.env, capture_output=True, timeout=10)
        self.assertNotEqual(result.returncode, 0)
        self.assertTrue((self.directory / ".deploy-lock").is_dir())
        self.assertEqual(json.loads(self.state.read_text())["calls"], [])


if __name__ == "__main__":
    unittest.main()
