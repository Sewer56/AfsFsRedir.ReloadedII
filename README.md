<div align="center">
	<h1>CRIWARE AFS Archive Redirector</h1>
	<img src="https://i.imgur.com/BjPn7rU.png" width="150" align="center" />
	<br/> <br/>
	<strong>At the Filesystem Level!?!<br/>
	Are you kidding me?<br/></strong>
<b>Id: reloaded.utils.afsredirector</b>
</div>

# CRIWARE AFS Redirector

Archived. Superseded by [https://github.com/Sewer56/FileEmulationFramework](FileEmulationFramework).

This is an experimental project and completely functional proof of concept mod for Reloaded II. Manipulating and redirecting accesses to contents of AFS archives with *ZERO* knowledge of the target application's code.

# Table of Contents
- [Supported Applications](#supported-applications)
- [How to Use](#how-to-use)
		- [Example(s)](#examples)
- [How it Works (Technical Summary)](#how-it-works-technical-summary)
	- [Preface](#preface)
	- [Operation (Simplified)](#operation-simplified)

# Supported Applications

In theory, everything. 
Every single Windows process accessing AFS archives should be affected.

In practice, it's probably close to that. This has been tested with the following:
- Sonic Heroes (PC)
- Sonic Adventure 2 (PC)
- Shadow The Hedgehog (Japamese, GameCube, Dolphin Emulator, Running from FileSystem)
- Some Random Old AFS Archive Extraction Utility (Why not?)

# How to Use

A. Add a dependency on this mod in your mod configuration.

```json
"ModDependencies": ["reloaded.utils.afsredirector"]
```

B. Add a folder called `AfsRedirector` in your mod folder.
C. Make folders corresponding to AFS Archive names, e.g. `SH_VOICE_E.AFS`.

Files inside AFS Archives are accessed by index, i.e. order in the archive: 0, 1, 2, 3 etc.
Inside each folder make files, with names corresponding to the file's index.

### Example(s)

To replace a file in an archive named `EVENT_ADX_E.AFS`...

Adding `AfsRedirector/EVENT_ADX_E.AFS/0.adx` to your mod would replace the 0th item in the original AFS Archive.

Adding `AfsRedirector/EVENT_ADX_E.AFS/32.aix` to your mod would replace the 32th item in the original AFS Archive.

**Note 1:**
Generally, for audio playback, you can place ADX/AHX/AIX files interchangeably, e.g. You can place a `32.adx` file even if the original AFS archive has an AIX file inside in that slot. 

**Note 2:** A common misconception is that AFS archives can only be used to store audio. This is in fact wrong. AFS archives can store any kind of data, it's just that they're almost exclusively used to store audio.


# How it Works (Technical Summary)

*Essentially, this project emulates real AFS archives at the Windows API level.*

## Preface
On Windows, when an application wants to operate on a file, a `handle` must first be opened through the use of the `CreateFile` API and/or its derivatives. All derivatives will essentially, at some point call the internal `NtCreateFile` API. 

Subsequently for reading information from files, `ReadFile` is used (and its derivatives) to read data from files to a buffer. These essentially at some point call `NtReadFile`.

## Operation (Simplified)

**Creating Virtual Archives**
This project hooks, `NtCreateFile`. When `NtCreateFile` is called, the file is checked to be an AFS (by first checking extension, then 4 bytes of header if extension matches).

If the header check succeeds, a `"Virtual AFS"` is built. The contents of the full original AFS file header are read in. Then, a new AFS header is built (for an archive that does not actually exist!) based upon the original header and the new files requested from other mods. 

This header is a completely valid header and would represent an AFS archive if it was repacked in with the new files.

A dictionary is also created, which maps file offsets in the `"Virtual AFS"` to file offsets and lengths in either the original AFS file or custom external files 

**Emulating Archives**
After creating a `"Virtual AFS"`, its contents need to be read in by the application.
To achieve this, `NtReadFile` is hooked to feed custom information.

If the process wants to read from an AFS file, for which a `"Virtual AFS"`exists, the call to the original `NtReadFile` function is skipped. Instead, custom data is written to the buffer.

If the requested read offset is between the start and end of the header, data from the `"Virtual AFS"` header at that offset is returned.

If the requested read offset is outside of the header, the offset is looked up against the dictionary in the `"Virtual AFS"`, and data is returned either directly from the original AFS file or an external source.
