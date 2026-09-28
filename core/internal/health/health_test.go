package health

import (
	"strings"
	"testing"

	"myarch/internal/render"
)

func TestRun(t *testing.T) {
	var results []Result
	for _, c := range []render.Check{
		{Plugin: "a", Name: "passes", Run: "true", Timeout: 5},
		{Plugin: "b", Name: "fails", Run: "echo broken; exit 3", Timeout: 5},
		{Plugin: "c", Name: "hangs", Run: "sleep 10", Timeout: 1},
	} {
		results = append(results, RunOne(c))
	}

	if !results[0].OK || results[0].Output != "" {
		t.Errorf("passing check: %+v", results[0])
	}
	if results[1].OK || results[1].Output != "broken" {
		t.Errorf("failing check should keep its output: %+v", results[1])
	}
	if results[2].OK || !strings.Contains(results[2].Output, "timed out") || results[2].Took.Seconds() > 3 {
		t.Errorf("hanging check should time out: %+v", results[2])
	}
	if f := Failed(results); len(f) != 2 {
		t.Errorf("Failed = %d, want 2", len(f))
	}
}

func TestSessionChecksAreSkippedOutsideIt(t *testing.T) {
	t.Setenv("HYPRLAND_INSTANCE_SIGNATURE", "")
	r := RunOne(render.Check{Name: "bar", Run: "exit 1", Timeout: 5, Session: true})
	if !r.OK || !r.Skipped {
		t.Fatalf("outside the session a session check must be skipped, not failed: %+v", r)
	}
}

func TestChildKeepingOutputOpenIsNotAFailure(t *testing.T) {
	// Like a check that restarts a daemon: it exits 0, the child lives on.
	r := RunOne(render.Check{Name: "restarts", Run: "sleep 5 & exit 0", Timeout: 10})
	if !r.OK {
		t.Fatalf("a passing check with a child left running must pass: %+v", r)
	}
}
