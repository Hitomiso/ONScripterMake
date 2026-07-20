# ONScripter Make
**A "build system" with built-in linter for ONScripter-RU (by Umineko Project) game scripts.**

## How does this work?
- Reads a list of files to be included in an output script
- Parses all lines in files, executes preprocessor directives, prints found parsing errors
- Saves definitions (aliases, labels, commands) and checks engine command calls for any errors
- Concatenates processed files into an output script

## How to use?
1. Create file 'ONSMakeList.txt' with a list of files - parts of the final script
2. Create file 'ONSMakeConfig.json' to use text replacement and command checking functions (please check example below)
3. Execute ONSMake from the CLI or a script
`onsmake -l <list file> -c <config file> -o 0.txt`
`onsmake -o 0.txt`

## Command line options
```
-?
--help
        Print help message.
-o <output file>
--output <output file>
        Specify output file name. Must be set in CLI options or config file.
-c <config file>
--config <config file>
        Specify JSON config file.
        Default value: ONSMakeConfig.json
        If the config file is not found, then all options must be set via the CLI.
-l <list file>
--list <list file>
        Specify the file with the list of files to be included into the output script.
        Default value: ONSMakeList.txt
-w <directory>
--work-dir <directory>
        Specify working directory.
-s (-y|-n)
--silent (-y|-n)
        Do not print messages to the console. When set, the -y or -n option must also be set.
-y
        Overwrite output file.
-n
        Do not overwrite output file.
-j <jobs count>
--jobs <jobs count>
        Specify number of threads used for processing files. Uses all available CPU threads by default.
-i <file path>
--input <file path>
        Specify files to include in the output script; each should have the -i option.
        Can be used when the list file does not exist. If there is no --list, then only -i files are included, even if default list file exists.
-r
--raw
        Concatenate included files without processing.
--ignore-directives
        Do not execute directives. They won't be deleted or commented.
--no-string-replaces
        Do not replace strings with patterns from config file.
--no-command-check
        Do not check output script for command call errors. Parsing and multiple definition errors will still be checked and printed.
--ignore-errors
        Write the output file even when there are detected errors in script.
```

## Available preprocessor directives
- `#dlg_autolabel *<label prefix>`
	Automatically inserts unique labels with the given prefix before all `d` and `d2` commands.
	This is useful for the complex log system in Umineko Project.
- `#dlg_autolabel off`
	Disables auto-insertion of labels before `d` and `d2` commands.
- `#*<label prefix>`
	Inserts a unique label with the given prefix in place of this directive.
- `#n <some dialog text>`
	Starts new page of NVL-dialog. After processing, it becomes a `d2` command call.
- `#e <some dialog text>`
	Appends dialog from the #n directive. Lines between #n and #e are automatically surrounded by `wait_on_d` and `d_continue`.
- `#end`
	Disables NVL-dialog mode, converts all added dialog parts into a single `d2` command call.

## ONSMakeList.txt example
```
# Directory separator is different on different operating systems
# Use \ on Windows
# Use / on *nix-like systems
define\define.ons
define\sprites.txt
define\variables.txt
define\backgrounds.txt
define\en\straliases.txt
# subroutines declarations
define\subroutines.txt

# *start is here
start.txt
main_menu.txt

# sample text
story\episode1.txt
story\episode2.txt
story\episode3.txt
story\episode4.txt

# subroutines definitions
end\commands.txt
# other routines
end\other.txt
```

## ONSMakeConfig.json example
When executing ONSMake without options, missing options will be taken from the config file.
In the "commands" property there is a path to a file with default engine commands definitions. It is needed to perform command call checking.
An example of the ons-ru.json file is in the examples directory.
In the "replaces" property there are tuples with replacement RegEx patterns ["what to change", "what to paste"].
```
{
	"list": "ListFile.txt",
	"output": "0.txt",
	"commands": "ons-ru.json",
	"inputs": ["appendix.txt"],
	"options": ["-y", "--ignore-errors"],
	"replaces": [
		["\${MODE}", "1920w"]
	]
}
```

## Preprocessor directives usage example
```arm
#dlg_autolabel *d
seplay 1,10,80
wait 800
seplay 1,11,80
#n `I trusted her.`[@]
seplay 1,10,80
wait 800
seplay 1,11,80
#e `{n}{n}...No, I still trust her.`[@]
seplay 1,10,80
wait 800
seplay 1,11,80
#e `{n}{n}Even in this very moment, I trust her.`[\]
#end
#dlg_autolabel off

#*d
d `...Somebody has been apologizing for a while now.`[\]
#*txt
d `I wonder what she's apologizing for.`[\]
```

## TODO
1. Add support for custom commands in the linter.
2. Optimize error handling in the lexer and parser.
3. Add support for JSON5.
4. Add a CLI option to specify the commands file.
5. Support for error message localization.
