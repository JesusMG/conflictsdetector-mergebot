# conflictsdetector-mergebot
ConflictsBot: A Plastic SCM DevOps mergebot that detects and reports merge conflicts of task branches with trunk branch at an early stage.

## Brief summary: How it works
It runs dry-merge operations to detect conflicts on every 'resolved' task branch with the trunk branch every time a new changeset is created in the trunk branch.

It's a perfect partner for Plastic SCM's built-in TrunkBot

## Compile ConflictsBot
ConflictsBot it's a dotnet core console app. So you will need dotnet installed in your computer to compile the sources.
Get it at the following [link](https://www.microsoft.com/net/download)

Once it's installed, download this project onto your computer and use the `dotnet`tool to compile, targeting your desired platform (windows-x64 in the example below):
* `$>git clone  https://github.com/JesusMG/conflictsdetector-mergebot .`
* `$>cd ConflictsBot`
* `$>dotnet publish -c Release -o bin\windows -r win-x64`

## A full configuration example
This section breaks down the configuration of the Plastic SCM DevOps feature for a repo with a Trunk Builder mergebot (TrunkBot) and a ConflitsDetector mergebot (ConflictsBot) working together.
The initial step is to have a Plastic repo filled with the project code I want to track. In this example, my repo name is **pnunit**, and my Plastic server is configured with User/Password authentication mode. A special “bot” user was created for the mergebots described above.

### 1-The plugs
Plugs are mergebot connectors to actual external systems supporting the build & merge process. In this example, I used:
* **Jira** as the issue tracker (optional)
* **Jenkins** to build & test the project code (mandatory)
* An **email** notifier based on Gmail (optional)

The configuration of these plugs is pretty easy: they just require URL’s to actual services, and credentials to access them.

![DevOps plugs](https://image.ibb.co/fmDAqA/00-plugs.png)

### 2-The TrunkBot
TrunkBot is the Plastic SCM's DevOps built-in mergebot that drives the build & merge process for the tracked branches of selected repo. I created mine with the following configuration by clicking the *Add a new mergebot* button:

![New Mergebot - TrunkBot - Page 1](https://image.ibb.co/b2gwVA/01-new-trunkbot-1.png)

No surprises here. I typed the repo name to track, I selected which branch will have the role of trunk branch (**/main**), the task branches prefix to track & merge to trunk branch, and the Plastic user for the TrunkBot in order to query and execute operations on the tracked repo.
Next, I defined which is the attribute name to identify the “status” of a branch, and its possible values:

![New Mergebot - TrunkBot - Page 2](https://image.ibb.co/choMxq/01-new-trunkbot-2.png)

As you can see in the screenshot above, I also configured the *automatic labeling* feature to create a new auto-increment label on every new changeset in the trunk branch.

And finally, the configuration of the TrunkBot’s supporting plugs. 
Jira Issue tracker: I typed the project key (the same as the branch prefix but without the *"-"* character, so the branch names match with Jira Issues) and the possible status values:

![New Mergebot - TrunkBot - Page 3](https://image.ibb.co/j2OX3V/01-new-trunkbot-3.png)

Remember, a task branch won’t be processed by TrunkBot until the branch status attribute is set to *resolved*, and the related Jira issue status is set to *Reviewed*.

Jenkins: I just typed the job names to be run by TrunkBot to build & test the merged code of the task branch with trunk branch before checking-in. If this job is executed successfully, the merged code is checked-in and an extra job can be optionally triggered before processing the next task branch (useful to deploy the recently checked-in task branch):

![New Mergebot - TrunkBot - Page 4](https://image.ibb.co/dd8ziV/01-new-trunkbot-4.png)

And, the Email notification plug is very simple: I just typed the Plastic profile field to get the email of the task branch owner to perform notifications, plus a fixed list of recipients (Plastic user names or actual email addresses can be entered this way).

#### Extra: Mergebot plug-in on CI (Jenkins) side
The machine hosting the Jenkins server/agents needs a valid Plastic SCM command line client installation, plus the *mergebot* plugin component. This way, Jenkins is able to download the merged code the TrunkBot takes from Plastic server to the Jenkins job’s workspace prior to running the job steps.

In case of Jenkins, just search & install the *mergebot* component.

![Jenkins side - mergebot plugin - install](https://image.ibb.co/eD73AA/02-jenkins-mergebot-install.png)

Then, the *Source Code management* option must be set to *Mergebot Plastic SCM* in the Jenkins jobs triggered by TrunkBot.

![Jenkins side - mergebot plugin - configuration](https://image.ibb.co/bP8AqA/03-jenkins-project-config.png)

#### Progress so far: TrunkBot on its full glory
At this stage, TrunkBot is fully functional and ready to process task branches. The screenshot below shows the resultant Branch Explorer after TrunkBot processed the first task branch and the merged code was successfully built in Jenkins:

![TrunkBot - First integration Cycle](https://image.ibb.co/dMeuHq/04-Trunk-Bot-First-Cycle.png)

Since the build was successful, TrunkBot checked-in the merged code in **/main** branch, it turned the branch status attribute to *merged* value, as well as the related Jira issue status. Then, it labeled the resultant changeset with an automatic label name: **REL_1.0**. Finally, TrunkBot triggered the configured *post-check-in job* in Jenkins.

### 3- The ConflictsBot
Now, let’s suppose the project evolves into a more complex scenario, where several users work concurrently on different task branches:

![TrunkBot - Second integration Cycle](https://image.ibb.co/iV4iAA/05-Trunk-Bot-Second-Cycle.png)

As you can see, a new branch (**/main/PNU-3**) was processed by TrunkBot and successfully merged in **/main** branch.
Let’s suppose this branch carries several remarkable changes in the project to implement a feature. And therefore, its integration possibly causes other “resolved” queued branches (colored in blue in the screenshot above) to require manual user intervention during their merge. 

One of these affected branches is **/main/PNU-5**. And let’s also suppose TrunkBot will process  **/main/PNU-2** and **/main/PNU-4** before, which don’t have any manual conflicts with **/main** branch so far.
With the current setup (no *ConflictsBot* yet), we will have to wait until **/main/PNU-2** and **/main/PNU-4** are processed by TrunkBot to notice the branch **/main/PNU-5** requires resolving manual conflicts with **/main** branch.

But, since **/main/PNU-5** is already tagged with the *resolved* status attribute, we could anticipate the fact the branch requires manual conflict resolution on its merge to **/main** branch. Here’s where *ConflictsBot* comes into play.

#### Setup ConflictsBot
Download and compile ConflictsBot following the instructions above.
Once the bin files are ready, let’s use it in our Plastic server as a [custom mergebot](http://blog.plasticscm.com/2018/10/plastic-scm-devops-custom-mergebots.html).

Go to the *DevOps* section in *Plastic Server WebAdmin > Mergebot Types > Add custom mergebot type now*:

![Plastic DevOps - Add custom mergebot](https://image.ibb.co/kB8X3V/06-Add-Custom-Merge-Bot.png)

And then, complete the form with the proper paths depending on the location of your downloaded ConflictsBot project. No extra parameters are required in the `command line to start`. See the example below:

![Plastc DevOps - Configure custom mergebot type](https://image.ibb.co/khpGVA/07-Fill-Custom-Merge-Bot.png)

Now, we’re ready to configure an actual instance of ConflictsBot.
Go to the *DevOps* section in *Plastic Server WebAdmin > Dashboard > Add a new MergeBot*.
The configuration values regarding the repo to track, *trunk* branch and task branches prefix are the same as TrunkBot:

![Plastc DevOps - New ConflictsBot instance - Page 1](https://image.ibb.co/bUmwVA/08-New-Conflicts-Bot-1.png)

Next, regarding the *status* attribute of task branches, the recommended configuration is to set the same values as TrunkBot for at least for the following entries:
* The name of the *status* attribute.
* The *Resolved* value for the *status* attribute.
* The *Merged* value for the *status* attribute.

![Plastc DevOps - New ConflictsBot instance - Page 2](https://image.ibb.co/nvJX3V/08-New-Conflicts-Bot-2.png)

Regarding the Issue Tracker configuration, the recommendation here is to use the same configuration as TrunkBot. We can reuse the same Issue Tracker plug, and the same *status* value **except** for the *Resolved* value: Instead of having to wait for the *Reviewed* status for a task like we do in TrunkBot, we set an early status value in the workflow to trigger the ConflictsBot merge check. In this example, the value is *Implemented*:

![Plastc DevOps - New ConflictsBot instance - Page 3](https://image.ibb.co/dLyh3V/08-New-Conflicts-Bot-3.png)

In summary:  with this configuration, ConflictsBot will trigger the first conflicts-check for each task branch when the branch status attribute is set to *resolved*, and the related Jira status is set to *Implemented*.
Finally, with regards to ConflictsBot notifications, we can configure the fixed list of recipients and whether sending a message just when conflicts are detected, or in any conflicts-check performed, even on successful ones. In the example below I used email notifications with the following configuration:

![Plastc DevOps - New ConflictsBot instance - Page 4](https://image.ibb.co/fu6rxq/08-New-Conflicts-Bot-4.png)

And that’s it! ConflictsBot is ready to work and detect manual conflicts at earlier stages in the workflow!

#### ConflictsBot in its full glory

![Plastc DevOps - TrunkBot, ConflictsBot and plugs running](https://image.ibb.co/kpr23V/09-Conflits-Bot-Cycle1-1-dashboard.png)

Let’s see what happened since we started our new ConflictsBot in the **pnunit** repo:
If we open the report of processed branches of ConflictsBot (named **uyox** in the screenshot above), we can see it already checked the *resolved* branches (**PNU-2**, **PNU-4** and **PNU-5**):

![Plastc DevOps - ConflictsBot first cycle report](https://image.ibb.co/f48h3V/09-Conflits-Bot-Cycle1-2-report.png)

Because it detected that **/main/PNU-5** has conflicts that require user-intervention, ConflictsBot set  the branch attribute to **failed**, and the Jira status to *Open*. Also, an email notification is sent to the branch owner’s email and configured fixed recipients:

![Plastic BranchExplorer ConflictsBot first cycle diagram](https://image.ibb.co/kUyRVA/10-Conflits-Bot-Cycle-1plastic.png)

#### The value-added feature of ConflictsBot
But as described earlier, the benefits of ConflictsBot don’t finish here. Let’s continue with the example.
Since there are conflicts that require user intervention, I switch to branch **/main/PNU-5** in my Plastic workspace and I run a merge (rebase) from **/main**:

![Plastic Rebase branch - resolve manual conflicts](https://image.ibb.co/ehQvOV/11-Merge-Conflict-Cycle2.png)

Note how the merge operation shows the conflict that require manual user intervention: 
* *PNUnitTestRunner.cs* file modified a method in branch **/main/PNU-3** and then, the branch was merged to **/main**.
* But, **/main/PNU-5** deleted the entire file. 
I resolve the conflict by keeping the file deletion. 
Once I verify the resultant merged code compiles successfully in my workspace, I check-in the changes and I set the branch attribute to *resolved* again, and the related Jira Issue’s status to *Implemented*:

![Plastic BranchExplorer Rebased branch diagram](https://image.ibb.co/c3th3V/12-Merge-Conflict-Cycle2-rebase.png)

ConflictsBot triggers another conflict check for branch **/main/PNU-5**:

![Plastc DevOps - ConflictsBot second cycle report](https://image.ibb.co/cww23V/13-Merge-Conflict-Cycle2-rebase-report-reprocess.png)

And, this time no manual conflicts were detected.
But… did you notice the branch **/main/PNU-4** turned to yellow in the Branch Explorer screenshot? That means TrunkBot picked it in the meantime we were fixing our conflicting branch **/main/PNU-5**.  And, TrunkBot finally succeeds:

![Plastic BranchExplorer ConflictsBot second cycle diagram](https://image.ibb.co/eXotAA/14-Merge-Conflict-Cycle2-new-branch-merged.png)

And here’s where ConflictsBot really shines (explanation below the screenshot):

![Plastc DevOps - ConflictsBot report reprocess resolved branches on new trunk branch checkin](https://image.ibb.co/kxedcq/15-Merge-Conflict-Cycle2-re-process-on-new-main-sets.png)

Several actions happened since the previous report:
* TrunkBot successfully merged **/main/PNU-4** into **/main** branch, and hence, it set the branch “status” attribute to *merged*.
* Since the branch status was set to *merged*, ConflictsBot removes **/main/PNU-4** from its queue, and won’t be processed again (it makes sense, as it was already merged).
* A new changeset was created in **/main** branch as a result of merging branch **/main/PNU-4**.
* ConflictsBot detects this new changeset in the **/main** branch, and triggers a new merge conflicts check for the remaining queued, *resolved* tasks: **/main/PNU-2** and **/main/PNU-5**.
In this case, no more manual conflicts appeared as the report shows. But, if new changesets are created in **/main** branch, and they cause manual conflicts with *resolved* but pending to integrate branches, the branch owner is posted instantly!

And that’s all! Hopefully, this DevOps setup helps you improving the speed of your deployment pipeline by preventing rejection of code at latter stages because of manual merge conflicts.
