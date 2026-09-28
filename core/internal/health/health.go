// Package health runs the checks plugins declare: shell commands that exit
// 0 when what the plugin is responsible for works.
package health

import (
	"bytes"
	"context"
	"errors"
	"os"
	"os/exec"
	"strings"
	"time"

	"myarch/internal/render"
)

type Result struct {
	Plugin  string        `json:"plugin"`
	Name    string        `json:"name"`
	OK      bool          `json:"ok"`
	Skipped bool          `json:"skipped,omitempty"` // needs the session, which isn't there
	Output  string        `json:"output,omitempty"`  // only kept when it failed
	Took    time.Duration `json:"took"`
}

// InSession tells whether we run inside the graphical session: Hyprland's
// instance and a Wayland display are there.
func InSession() bool {
	return os.Getenv("HYPRLAND_INSTANCE_SIGNATURE") != "" && os.Getenv("WAYLAND_DISPLAY") != ""
}

// RunOne runs a single check under its timeout. Callers run checks one
// after the other: some restart things other checks look at.
func RunOne(c render.Check) Result {
	if c.Session && !InSession() {
		return Result{Plugin: c.Plugin, Name: c.Name, OK: true, Skipped: true}
	}
	ctx, cancel := context.WithTimeout(context.Background(), time.Duration(c.Timeout)*time.Second)
	defer cancel()
	cmd := exec.CommandContext(ctx, "sh", "-c", c.Run)
	var out bytes.Buffer
	cmd.Stdout = &out
	cmd.Stderr = &out
	// Don't wait on children the check left behind (a shell it restarted).
	cmd.WaitDelay = time.Second
	start := time.Now()
	err := cmd.Run()
	// A child the check left running (a daemon it restarted) can hold the
	// output open past WaitDelay; the check itself still passed.
	if errors.Is(err, exec.ErrWaitDelay) && cmd.ProcessState != nil && cmd.ProcessState.Success() {
		err = nil
	}
	r := Result{Plugin: c.Plugin, Name: c.Name, OK: err == nil, Took: time.Since(start)}
	if err != nil {
		msg := strings.TrimSpace(out.String())
		if errors.Is(ctx.Err(), context.DeadlineExceeded) {
			msg = strings.TrimSpace(msg + "\n(timed out after " + (time.Duration(c.Timeout) * time.Second).String() + ")")
		}
		r.Output = lastLines(msg, 6)
	}
	return r
}

func lastLines(s string, n int) string {
	lines := strings.Split(s, "\n")
	if len(lines) > n {
		lines = lines[len(lines)-n:]
	}
	return strings.Join(lines, "\n")
}

// Failed returns the results that didn't pass.
func Failed(results []Result) []Result {
	var out []Result
	for _, r := range results {
		if !r.OK {
			out = append(out, r)
		}
	}
	return out
}
