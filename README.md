# Extended Rating List

This is a C# console app using [Spectre.Console](https://spectreconsole.net/) that functions as an extended version of [vivid/stasis](https://store.steampowered.com/app/2093940/vividstasis/)'s rating list feature.  
In vivid/stasis, the Rating List displays your top 30 best plays sorted by rate, but you cannot scroll past 30 plays. This app displays your best plays in a format similar to the game's rating list, *and* lets you scroll past the 30th slot.  
Additionally, this app also contains a mode that lets you see your progress on your *completion score* (another game mechanic related to rating) in a similar fashion. *I wanted to create a mode to display EX ratings as well, but I never got around to it as I never needed it.*

The code in this program is currently Very Bad, but I wrote all of it myself and it works ~~(on my machine, at least)~~ so I don't care that much.

## Usage

For the app to properly display your plays, it needs to read two files from the game: __highscore_table__ and __song_information.bin__. `highscore_table` is in your *save file*, while `song_information.bin` is in the *game's files* (located next to `VIVIDSTASIS.exe`).  
The app tries to look for these files automatically (it *should* be able to find them if you're on Windows), but if it can't, you can give it the paths to these files manually by creating text files `highscorepath.txt` and `songinformationpath.txt` next to the program, containing the filepaths of the files.

If everything works correctly, upon opening the app you should be greeted with your rating list. You can now type in various commands into the console to scroll through the rating list, switch modes, etc.  
You enter a command by typing that command then pressing Enter. The app should print the controls into the console, but here they are here:

`b<number>`: Shows the *top <number>* entries of your rating list. *The default view can be obtained by entering `b30`.* This command resets both how much you've scrolled, and how large one page is.  
`#<number>`: Sets your current scroll position to the #<number> spot.  
`p<number>`: Changes the size of one page, or how many entries are displayed at the same time. This keeps your position at the same spot.  
`w`: Scrolls up by the size of one page. If scrolling up would cause you to scroll past your best play, it caps your scroll position to #1.  
`e`: Scrolls down by the size of one page. If you are currently viewing entries 1–30, entering `e` will then show entries 31–60.  

`z`: Sets the current view/mode to Rating List.  
`x`: ~~Sets the current view/mode to EX Rating List.~~ Currently does nothing; I have not created this view yet.  
`c`: Sets the current view/mode to Completion view.  

Entering `.` then hitting Enter will close the program.

## How to read the Completion view / how Completion works

Your Completion score is a number from 0 to 1 calculated using your scores on all of the songs across the game. You get a completion score for each song, and the score for each song is totaled to calculate your total Completion score.  
The Completion mode takes every song in the game, and sorts them by your completion scores. The completion score displayed is rescaled to be from 0 to 100, where 100 is the maximum completion you can get on a song.  
The completion score for a song is calculated by taking your scores on all difficulties of a song, and averaging them together. The higher the chart scores, the higher the completion score.  
However, if a certain difficulty has a lower score than a higher difficulty on the same song, the score for that difficulty will be treated as if it was equal to the higher difficulty's score.  
*For example, if you get a VS (the maximum score) on the Encore for a song, then that song will have maximum completion, even if you have lower scores on every other difficulty, as the lower scores will be treated as if they were also VS.*  
If a score is written in italics and dark lettering, that means that score is being overrided by a score from a higher difficulty. *(It may not show up correctly depending on the specifics of your terminal.)*  

The colors used for the completion score are taken from the same colors as the normal Rating List, although since there are no colors for completion scores in the actual game, I picked them out myself and they're a bit arbitrary.
